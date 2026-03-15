using System;

namespace Orion.Core.Services
{
    public class BlobMetadata
    {
        public string Path { get; set; } = string.Empty;
        public long Size { get; set; }
        public DateTime LastModified { get; set; }
        public string? ContentType { get; set; }
        public string? ETag { get; set; }
    }
}
