using System;
using System.Threading.Tasks;

namespace Orion.Core.Services
{
    public interface IStorageService
    {
        Task SaveBlobAsync(string path, byte[] data, string? contentType = null);
        Task<byte[]> GetBlobAsync(string path);
        Task DeleteBlobAsync(string path);
        Task<bool> ExistsAsync(string path);
        Task<System.Collections.Generic.IEnumerable<string>> ListBlobsAsync(string prefix);
        Task<BlobMetadata?> GetBlobMetadataAsync(string path);
    }
}
