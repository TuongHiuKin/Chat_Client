"""Exercise the built Linux image over real TCP; only creates disposable containers."""
import argparse
import base64
import hashlib
import json
import os
import secrets
import socket
import subprocess
import time
import uuid


def docker(*args, check=True):
    return subprocess.run(["docker", *args], check=check, text=True, capture_output=True)


class Peer:
    def __init__(self, port):
        self.socket = socket.create_connection(("127.0.0.1", port), timeout=15)
        self.stream = self.socket.makefile("rb")

    def send(self, kind, payload=None, conversation=None, content=None):
        request = uuid.uuid4().hex
        self.socket.sendall((json.dumps(dict(type=kind, requestId=request,
            payload=payload, conversationId=conversation, content=content)) + "\n").encode())
        return request

    def receive(self, kind=None, request=None):
        for _ in range(100):
            line = self.stream.readline(4 * 1024 * 1024)
            if not line:
                raise RuntimeError("Server disconnected")
            message = json.loads(line)
            if message["type"] == 99:
                raise RuntimeError(message.get("content", "Server error"))
            if (kind is None or message["type"] == kind) and (request is None or message.get("requestId") == request):
                return message
        raise RuntimeError("Expected response not received")

    def transfer(self, kind, payload, conversation=None):
        request = self.send(kind, payload, conversation)
        return self.receive(45, request)["payload"]

    def close(self):
        self.stream.close()
        self.socket.close()


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--image", required=True)
    parser.add_argument("--target", choices=["no-db", "all-in-one"], required=True)
    args = parser.parse_args()
    name = "chat-ci-" + uuid.uuid4().hex[:12]
    sql = name + "-sql"
    password = "Ci!" + secrets.token_hex(16) + "aA1"
    if os.environ.get("GITHUB_ACTIONS"):
        print("::add-mask::" + password, flush=True)
    peers = []
    try:
        docker("network", "create", name)
        if args.target == "no-db":
            docker("run", "-d", "--name", sql, "--network", name,
                "-e", "ACCEPT_EULA=Y", "-e", "MSSQL_PID=Developer", "-e", "MSSQL_SA_PASSWORD=" + password,
                "mcr.microsoft.com/mssql/server:2022-latest")
            # Wait for SQL before starting the standalone app's own retry window.
            for _ in range(90):
                ready = docker("exec", sql, "/opt/mssql-tools18/bin/sqlcmd", "-S", "localhost", "-U", "sa", "-P", password,
                    "-C", "-Q", "SELECT 1", check=False)
                if ready.returncode == 0:
                    break
                time.sleep(2)
            else:
                raise RuntimeError("SQL Server did not become ready")
            extra = ["-e", f"ConnectionStrings__DefaultConnection=Server={sql},1433;Database=ChatDB;User Id=sa;Password={password};TrustServerCertificate=True"]
        else:
            extra = ["-e", "MSSQL_SA_PASSWORD=" + password]
        docker("run", "-d", "--name", name, "--network", name, "-p", "127.0.0.1::5000", *extra, args.image)
        mapping = docker("port", name, "5000/tcp").stdout.strip()
        port = int(mapping.rsplit(":", 1)[1])
        for _ in range(90):
            try:
                with socket.create_connection(("127.0.0.1", port), timeout=1):
                    break
            except OSError:
                time.sleep(2)
        else:
            raise RuntimeError("Chat server did not become ready")
        for i in range(3):
            peer = Peer(port)
            peers.append(peer)
            peer.send(2, dict(username=f"ci-user-{i}", password=password, displayName=f"Tester {i}"))
            assert peer.receive(3)["payload"]["success"], "Registration failed"
        sender, receiver, third = peers
        sender.send(22, dict(type="group", name="Docker smoke", memberIds=[]))
        group = sender.receive(21)["conversationId"]
        for peer in (receiver, third):
            peer.send(46, conversation=group)
            assert peer.receive(21)["conversationId"] == group
        sender.send(10, conversation=group, content="Docker Lab2 😀")
        for peer in peers:
            assert peer.receive(11)["content"] == "Docker Lab2 😀"
        data = secrets.token_bytes(1024 * 1024)
        transfer = sender.transfer(40, dict(fileName="smoke.bin", fileSize=len(data), contentType="application/octet-stream"), group)["transferId"]
        for offset in range(0, len(data), 65536):
            sender.transfer(41, dict(transferId=transfer, offset=offset, dataBase64=base64.b64encode(data[offset:offset+65536]).decode()))
        sender.transfer(42, dict(transferId=transfer, sha256=hashlib.sha256(data).hexdigest()))
        attachment = receiver.receive(11)["payload"]["attachmentId"]
        actual = bytearray()
        while len(actual) < len(data):
            response = receiver.transfer(44, dict(attachmentId=attachment, offset=len(actual)))
            chunk = base64.b64decode(response["dataBase64"])
            assert chunk, "Empty download chunk"
            actual.extend(chunk)
        assert hashlib.sha256(actual).digest() == hashlib.sha256(data).digest(), "Checksum mismatch"
        print(f"PASS: {args.target}: SQL startup, 3-member chat, 1 MiB upload/download and SHA-256")
    except Exception:
        for container in (name, sql):
            logs = docker("logs", "--tail", "80", container, check=False)
            print((logs.stdout + logs.stderr).replace(password, "***"))
        raise
    finally:
        for peer in peers:
            peer.close()
        docker("rm", "-f", name, sql, check=False)
        docker("network", "rm", name, check=False)


if __name__ == "__main__":
    main()
