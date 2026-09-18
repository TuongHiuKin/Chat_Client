#!/bin/bash
set -Eeuo pipefail

: "${MSSQL_SA_PASSWORD:?Set MSSQL_SA_PASSWORD when starting the all-in-one image.}"
# Quote the SQL password as a connection-string value, including embedded quotes.
quoted_password=${MSSQL_SA_PASSWORD//\"/\"\"}
export ConnectionStrings__DefaultConnection="Server=127.0.0.1,1433;Database=ChatDB;User Id=sa;Password=\"${quoted_password}\";TrustServerCertificate=True"

sql_pid=""
app_pid=""
stop_services() {
    trap - TERM INT EXIT
    for pid in "$app_pid" "$sql_pid"; do
        if [[ -n "$pid" ]]; then kill -TERM "$pid" 2>/dev/null || true; fi
    done
    wait || true
}
trap stop_services TERM INT EXIT

/opt/mssql/bin/sqlservr &
sql_pid=$!
# Give a fresh SQL volume enough time to initialize before the app's retries begin.
ready=false
for attempt in {1..90}; do
    if ! kill -0 "$sql_pid" 2>/dev/null; then
        echo "SQL Server exited during startup." >&2
        exit 1
    fi
    if SQLCMDPASSWORD="$MSSQL_SA_PASSWORD" /opt/mssql-tools18/bin/sqlcmd \
        -S localhost -U sa -C -l 2 -Q 'SELECT 1' >/dev/null 2>&1; then
        ready=true
        break
    fi
    sleep 2
done
if [[ "$ready" != true ]]; then
    echo "SQL Server did not become ready in time." >&2
    exit 1
fi
cd /app
./ChatServer &
app_pid=$!

# Stop the other service if either SQL Server or ChatServer exits.
set +e
wait -n "$sql_pid" "$app_pid"
status=$?
set -e
exit "$status"
