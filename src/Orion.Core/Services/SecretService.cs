using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Orion.Core.Services
{
    public interface ISecretService
    {
        Task<Dictionary<string, string>> GetSecretsAsync(Guid appId, bool decrypt = false, string? userId = null);
        Task SetSecretAsync(Guid appId, string key, string value);
        Task DeleteSecretAsync(Guid appId, string key);
    }

    public class SecretService : ISecretService
    {
        private readonly IMetadataService _db;
        private readonly ISecurityService _security;

        public SecretService(IMetadataService db, ISecurityService security)
        {
            _db = db;
            _security = security;
        }

        public async Task<Dictionary<string, string>> GetSecretsAsync(Guid appId, bool decrypt = false, string? userId = null)
        {
            var encryptedSecrets = await _db.GetSecretsAsync(appId, userId);
            if (!decrypt) return encryptedSecrets;

            var decryptedSecrets = new Dictionary<string, string>();
            foreach (var kvp in encryptedSecrets)
            {
                try
                {
                    decryptedSecrets[kvp.Key] = _security.Decrypt(kvp.Value);
                }
                catch
                {
                    decryptedSecrets[kvp.Key] = "[DECRYPTION_FAILED]";
                }
            }
            return decryptedSecrets;
        }

        public async Task SetSecretAsync(Guid appId, string key, string value)
        {
            var encryptedValue = _security.Encrypt(value);
            await _db.SaveSecretAsync(appId, key, encryptedValue);
        }

        public async Task DeleteSecretAsync(Guid appId, string key)
        {
            await _db.DeleteSecretAsync(appId, key);
        }
    }
}
