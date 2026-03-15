using System;
using System.Threading.Tasks;

namespace Orion.Core.Services
{
    public interface IStorageService
    {
        Task SaveBlobAsync(string path, byte[] data);
        Task<byte[]> GetBlobAsync(string path);
        Task DeleteBlobAsync(string path);
        Task<bool> ExistsAsync(string path);
    }
}
