using System;
using System.IO;
using System.Collections.Generic;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;

namespace Orion.Core.Services
{
    public class AzureBlobStorageService : IStorageService
    {
        private readonly ILogger<AzureBlobStorageService> _logger;
        private readonly BlobContainerClient _containerClient;

        public AzureBlobStorageService(ILogger<AzureBlobStorageService> logger, IConfiguration configuration)
        {
            _logger = logger;
            var connectionString = configuration["Orion:Storage:Azure:ConnectionString"];
            var containerName = configuration["Orion:Storage:Azure:ContainerName"] ?? "orion-storage";

            var blobServiceClient = new BlobServiceClient(connectionString);
            _containerClient = blobServiceClient.GetBlobContainerClient(containerName);
            _containerClient.CreateIfNotExists();
        }

        public async Task SaveBlobAsync(string path, byte[] data, string? contentType = null)
        {
            var blobClient = _containerClient.GetBlobClient(path.Replace("\\", "/"));
            using var stream = new MemoryStream(data);
            var options = new BlobUploadOptions {
                HttpHeaders = new BlobHttpHeaders { ContentType = contentType }
            };
            await blobClient.UploadAsync(stream, options);
        }

        public async Task<byte[]> GetBlobAsync(string path)
        {
            var blobClient = _containerClient.GetBlobClient(path.Replace("\\", "/"));
            var download = await blobClient.DownloadContentAsync();
            return download.Value.Content.ToArray();
        }

        public async Task DeleteBlobAsync(string path)
        {
            var blobClient = _containerClient.GetBlobClient(path.Replace("\\", "/"));
            await blobClient.DeleteIfExistsAsync();
        }

        public async Task<bool> ExistsAsync(string path)
        {
            var blobClient = _containerClient.GetBlobClient(path.Replace("\\", "/"));
            return await blobClient.ExistsAsync();
        }

        public async Task<IEnumerable<string>> ListBlobsAsync(string prefix)
        {
            var blobs = new List<string>();
            await foreach (var blob in _containerClient.GetBlobsAsync(prefix: prefix.Replace("\\", "/")))
            {
                blobs.Add(blob.Name);
            }
            return blobs;
        }

        public async Task<BlobMetadata?> GetBlobMetadataAsync(string path)
        {
            var blobClient = _containerClient.GetBlobClient(path.Replace("\\", "/"));
            if (!await blobClient.ExistsAsync()) return null;

            var properties = await blobClient.GetPropertiesAsync();
            return new BlobMetadata
            {
                Path = path,
                SizeBytes = properties.Value.ContentLength,
                LastModified = properties.Value.LastModified.UtcDateTime,
                ContentType = properties.Value.ContentType,
                ETag = properties.Value.ETag.ToString()
            };
        }
    }
}
