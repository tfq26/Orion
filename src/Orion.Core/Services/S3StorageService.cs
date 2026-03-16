using System;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;

namespace Orion.Core.Services
{
    public class S3StorageService : IStorageService
    {
        private readonly ILogger<S3StorageService> _logger;
        private readonly IAmazonS3 _s3Client;
        private readonly string _bucketName;

        public S3StorageService(ILogger<S3StorageService> logger, IConfiguration configuration)
        {
            _logger = logger;
            var accessKey = configuration["Orion:Storage:S3:AccessKey"];
            var secretKey = configuration["Orion:Storage:S3:SecretKey"];
            var region = configuration["Orion:Storage:S3:Region"] ?? "us-east-1";
            var serviceUrl = configuration["Orion:Storage:S3:ServiceUrl"];

            _bucketName = configuration["Orion:Storage:S3:BucketName"] ?? "orion-storage";

            var config = new AmazonS3Config();
            if (!string.IsNullOrEmpty(serviceUrl))
            {
                config.ServiceURL = serviceUrl;
                config.ForcePathStyle = true;
            }
            else
            {
                config.RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(region);
            }

            _s3Client = new AmazonS3Client(accessKey, secretKey, config);
        }

        public async Task SaveBlobAsync(string path, byte[] data, string? contentType = null)
        {
            using var stream = new MemoryStream(data);
            var request = new PutObjectRequest
            {
                BucketName = _bucketName,
                Key = path.Replace("\\", "/"),
                InputStream = stream,
                ContentType = contentType
            };
            await _s3Client.PutObjectAsync(request);
        }

        public async Task<byte[]> GetBlobAsync(string path)
        {
            var request = new GetObjectRequest
            {
                BucketName = _bucketName,
                Key = path.Replace("\\", "/"),
            };

            using var response = await _s3Client.GetObjectAsync(request);
            using var memoryStream = new MemoryStream();
            await response.ResponseStream.CopyToAsync(memoryStream);
            return memoryStream.ToArray();
        }

        public async Task DeleteBlobAsync(string path)
        {
            var request = new DeleteObjectRequest
            {
                BucketName = _bucketName,
                Key = path.Replace("\\", "/")
            };
            await _s3Client.DeleteObjectAsync(request);
        }

        public async Task<bool> ExistsAsync(string path)
        {
            try
            {
                await _s3Client.GetObjectMetadataAsync(_bucketName, path.Replace("\\", "/"));
                return true;
            }
            catch (AmazonS3Exception e) when (e.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return false;
            }
        }

        public async Task<IEnumerable<string>> ListBlobsAsync(string prefix)
        {
            var request = new ListObjectsV2Request
            {
                BucketName = _bucketName,
                Prefix = prefix.Replace("\\", "/")
            };
            var response = await _s3Client.ListObjectsV2Async(request);
            return System.Linq.Enumerable.Select(response.S3Objects, o => o.Key);
        }

        public async Task<BlobMetadata?> GetBlobMetadataAsync(string path)
        {
            try
            {
                var response = await _s3Client.GetObjectMetadataAsync(_bucketName, path.Replace("\\", "/"));
                return new BlobMetadata
                {
                    Path = path,
                    SizeBytes = response.ContentLength,
                    LastModified = response.LastModified,
                    ContentType = response.Headers.ContentType,
                    ETag = response.ETag
                };
            }
            catch (AmazonS3Exception e) when (e.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }
        }
    }
}
