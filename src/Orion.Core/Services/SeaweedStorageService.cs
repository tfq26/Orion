using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Orion.Core.Services
{
    public class SeaweedStorageService : IStorageService
    {
        private readonly ILogger<SeaweedStorageService> _logger;
        private readonly string _basePath;
        private readonly HttpClient _httpClient;
        private readonly string _filerUrl;

        public SeaweedStorageService(ILogger<SeaweedStorageService> logger, HttpClient httpClient)
        {
            _logger = logger;
            _httpClient = httpClient;
            // In a real setup, this would be the SeaweedFS Filer URL
            _filerUrl = Environment.GetEnvironmentVariable("SEAWEED_FILER_URL") ?? "http://localhost:8888";
            _basePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".orion_storage");
            
            if (!Directory.Exists(_basePath))
                Directory.CreateDirectory(_basePath);
        }

        public async Task SaveBlobAsync(string path, byte[] data)
        {
            _logger.LogInformation($"[STORAGE] Saving blob to {path} (Size: {data.Length} bytes)");
            
            // Simulation: Save to local directory
            var fullPath = Path.Combine(_basePath, path);
            var dir = Path.GetDirectoryName(fullPath);
            if (dir != null && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            await File.WriteAllBytesAsync(fullPath, data);

            // In real SeaweedFS, we would do:
            // var content = new ByteArrayContent(data);
            // await _httpClient.PostAsync($"{_filerUrl}/{path}", content);
        }

        public async Task<byte[]> GetBlobAsync(string path)
        {
            _logger.LogInformation($"[STORAGE] Retrieving blob from {path}");
            
            var fullPath = Path.Combine(_basePath, path);
            if (!File.Exists(fullPath))
                throw new FileNotFoundException($"Blob not found: {path}");

            return await File.ReadAllBytesAsync(fullPath);
        }

        public async Task DeleteBlobAsync(string path)
        {
            _logger.LogInformation($"[STORAGE] Deleting blob at {path}");
            var fullPath = Path.Combine(_basePath, path);
            if (File.Exists(fullPath))
                File.Delete(fullPath);
        }

        public Task<bool> ExistsAsync(string path)
        {
            var fullPath = Path.Combine(_basePath, path);
            return Task.FromResult(File.Exists(fullPath));
        }
    }
}
