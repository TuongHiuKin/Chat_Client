using System;
using System.Collections.Generic;
using System.Text;

namespace ChatServer.Models
{
    public class Attachment
    {
        public long AttachmentId { get; set; }

        public long MessageId { get; set; }

        public string FileName { get; set; }
            = string.Empty;

        public string ContentType { get; set; }
            = string.Empty;

        public long FileSize { get; set; }

        public byte[] FileData { get; set; }
            = Array.Empty<byte>();

        public Message Message { get; set; }
            = null!;
    }
}
