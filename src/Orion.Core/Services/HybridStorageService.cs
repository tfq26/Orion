using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Orion.Core.Services
{
    public class HybridStorageService : IStorageService
    {
        private readonly ILogger<HybridStorageService> _logger;
        private readonly IStorageService _localCache;
        private readonly IStorageService? _cloudProvider;
        private readonly bool _useCloud;

        public HybridStorageService(
            ILogger<HybridStorageService> logger, 
            IConfiguration configuration,
            IServiceProvider serviceProvider)
        {
            _logger = logger;
            _localCache = serviceProvider.GetRequiredService<LocalDiskStorageService>();

            var providerType = configuration["Orion:Storage:Provider"]?.ToUpper();
            _useCloud = !string.IsNullOrEmpty(providerType) && providerType != "LOCAL";

            if (_useCloud)
            {
                _cloudProvider = providerType switch
                {
                    "AWS" or "S3" => serviceProvider.GetRequiredService<S3StorageService>(),
                    "AZURE" or "BLOB" => serviceProvider.GetRequiredService<AzureBlobStorageService>(),
                    "GCP" or "GCS" => serviceProvider.GetRequiredService<GcpStorageService>(),
                    _ => throw new ArgumentException($"Unsupported storage provider: {providerType}")
                };
                _logger.LogInformation($"[HYBRID] Storage initialized with Cloud Provider: {providerType}");
            }
            else
            {
                _logger.LogInformation("[HYBRID] Storage initialized in LOCAL mode.");
            }
        }

        public async Task SaveBlobAsync(string path, byte[] data, string? contentType = null)
        {
            await _localCache.SaveBlobAsync(path, data, contentType);

            if (_useCloud && _cloudProvider != null)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _cloudProvider.SaveBlobAsync(path, data, contentType);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"[HYBRID] Failed to sync {path} to cloud.");
                    }
                });
            }
        }

        public async Task<byte[]> GetBlobAsync(string path)
        {
            if (await _localCache.ExistsAsync(path))
            {
                return await _localCache.GetBlobAsync(path);
            }

            if (_useCloud && _cloudProvider != null)
            {
                var data = await _cloudProvider.GetBlobAsync(path);
                var metadata = await _cloudProvider.GetBlobMetadataAsync(path);
                await _localCache.SaveBlobAsync(path, data, metadata?.ContentType);
                return data;
            }

            throw new System.IO.FileNotFoundException($"Blob not found: {path}");
        }

        public async Task DeleteBlobAsync(string path)
        {
            await _localCache.DeleteBlobAsync(path);
            if (_useCloud && _cloudProvider != null)
            {
                await _cloudProvider.DeleteBlobAsync(path);
            }
        }

        public async Task<bool> ExistsAsync(string path)
        {
            if (await _localCache.ExistsAsync(path)) return true;
            if (_useCloud && _cloudProvider != null) return await _cloudProvider.ExistsAsync(path);
            return false;
        }

        public async Task<IEnumerable<string>> ListBlobsAsync(string prefix)
        {
            // For listing, we usually want to merge or just list from cloud as the source of truth
            if (_useCloud && _cloudProvider != null)
            {
                return await _cloudProvider.ListBlobsAsync(prefix);
            }
            return await _localCache.ListBlobsAsync(prefix);
        }

        public async Task<BlobMetadata?> GetBlobMetadataAsync(string path)
        {
            var meta = await _localCache.GetBlobMetadataAsync(path);
            if (meta != null) return meta;

            if (_useCloud && _cloudProvider != null)
            {
                return await _cloudProvider.GetBlobMetadataAsync(path);
            }
            return null;
        }
    }
}
