using System;
using System.IO;
using System.Collections.Generic;
using System.Threading.Tasks;
using Google.Cloud.Storage.V1;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;

namespace Orion.Core.Services
{
    public class GcpStorageService : IStorageService
    {
        private readonly ILogger<GcpStorageService> _logger;
        private readonly StorageClient _storageClient;
        private readonly string _bucketName;

        public GcpStorageService(ILogger<GcpStorageService> logger, IConfiguration configuration)
        {
            _logger = logger;
            _bucketName = configuration["Orion:Storage:GCP:BucketName"] ?? "orion-storage";
            _storageClient = StorageClient.Create();
        }

        public async Task SaveBlobAsync(string path, byte[] data, string? contentType = null)
        {
            using var stream = new MemoryStream(data);
            await _storageClient.UploadObjectAsync(_bucketName, path.Replace("\\", "/"), contentType, stream);
        }

        public async Task<byte[]> GetBlobAsync(string path)
        {
            using var memoryStream = new MemoryStream();
            await _storageClient.DownloadObjectAsync(_bucketName, path.Replace("\\", "/"), memoryStream);
            return memoryStream.ToArray();
        }

        public async Task DeleteBlobAsync(string path)
        {
            await _storageClient.DeleteObjectAsync(_bucketName, path.Replace("\\", "/"));
        }

        public async Task<bool> ExistsAsync(string path)
        {
            try
            {
                await _storageClient.GetObjectAsync(_bucketName, path.Replace("\\", "/"));
                return true;
            }
            catch (Google.GoogleApiException e) when (e.Error.Code == 404)
            {
                return false;
            }
        }

        public async Task<IEnumerable<string>> ListBlobsAsync(string prefix)
        {
            var blobs = new List<string>();
            var objects = _storageClient.ListObjectsAsync(_bucketName, prefix.Replace("\\", "/"));
            await foreach (var obj in objects)
            {
                blobs.Add(obj.Name);
            }
            return blobs;
        }

        public async Task<BlobMetadata?> GetBlobMetadataAsync(string path)
        {
            try
            {
                var obj = await _storageClient.GetObjectAsync(_bucketName, path.Replace("\\", "/"));
                return new BlobMetadata
                {
                    Path = path,
                    Size = (long)(obj.Size ?? 0),
                    LastModified = obj.UpdatedDateTimeOffset?.UtcDateTime ?? DateTime.UtcNow,
                    ContentType = obj.ContentType,
                    ETag = obj.ETag
                };
            }
            catch (Google.GoogleApiException e) when (e.Error.Code == 404)
            {
                return null;
            }
        }
    }
}
