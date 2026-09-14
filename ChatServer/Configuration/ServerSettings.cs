using System;
using System.Collections.Generic;
using System.Text;

namespace ChatServer.Configuration
{
    public class ServerSettings
    {
        public string Host { get; set; } = string.Empty;

        public int Port { get; set; }
    }
}
