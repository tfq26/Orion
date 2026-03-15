using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Orion.Core.Services
{
    public class LocalDiskStorageService : IStorageService
    {
        private readonly ILogger<LocalDiskStorageService> _logger;
        private readonly string _basePath;

        public LocalDiskStorageService(ILogger<LocalDiskStorageService> logger)
        {
            _logger = logger;
            _basePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".orion_cache");
            if (!Directory.Exists(_basePath))
                Directory.CreateDirectory(_basePath);
        }

        public async Task SaveBlobAsync(string path, byte[] data, string? contentType = null)
        {
            var fullPath = GetFullPath(path);
            var dir = Path.GetDirectoryName(fullPath);
            if (dir != null && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            await File.WriteAllBytesAsync(fullPath, data);
            
            if (!string.IsNullOrEmpty(contentType))
            {
                await File.WriteAllTextAsync(fullPath + ".meta", contentType);
            }
        }

        public async Task<byte[]> GetBlobAsync(string path)
        {
            var fullPath = GetFullPath(path);
            if (!File.Exists(fullPath))
                throw new FileNotFoundException($"Cache miss: {path}");

            return await File.ReadAllBytesAsync(fullPath);
        }

        public Task DeleteBlobAsync(string path)
        {
            var fullPath = GetFullPath(path);
            if (File.Exists(fullPath))
                File.Delete(fullPath);
            
            if (File.Exists(fullPath + ".meta"))
                File.Delete(fullPath + ".meta");

            return Task.CompletedTask;
        }

        public Task<bool> ExistsAsync(string path)
        {
            return Task.FromResult(File.Exists(GetFullPath(path)));
        }

        public Task<System.Collections.Generic.IEnumerable<string>> ListBlobsAsync(string prefix)
        {
            var fullPath = GetFullPath(prefix);
            var dir = Path.GetDirectoryName(fullPath) ?? _basePath;
            var searchPattern = Path.GetFileName(fullPath) + "*";

            if (!Directory.Exists(dir)) return Task.FromResult(System.Linq.Enumerable.Empty<string>());

            var files = Directory.GetFiles(dir, searchPattern, SearchOption.AllDirectories);
            return Task.FromResult(System.Linq.Enumerable.Select(files, f => Path.GetRelativePath(_basePath, f).Replace("\\", "/")));
        }

        public async Task<BlobMetadata?> GetBlobMetadataAsync(string path)
        {
            var fullPath = GetFullPath(path);
            if (!File.Exists(fullPath)) return null;

            var info = new FileInfo(fullPath);
            string? contentType = null;
            if (File.Exists(fullPath + ".meta"))
            {
                contentType = await File.ReadAllTextAsync(fullPath + ".meta");
            }

            return new BlobMetadata
            {
                Path = path,
                Size = info.Length,
                LastModified = info.LastWriteTimeUtc,
                ContentType = contentType,
                ETag = $"\"{info.Length}-{info.LastWriteTimeUtc.Ticks}\""
            };
        }

        private string GetFullPath(string path) => Path.Combine(_basePath, path);
    }
}
