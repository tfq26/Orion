using Microsoft.AspNetCore.DataProtection;
using Orion.Core.Services;
using System.IO;
using System.Security.Cryptography;

namespace Orion.Api.Security
{
    public class DefaultHsmProvider : IHsmProvider
    {
        private readonly IDataProtector _protector;
        private readonly string _keyFilePath = "orion_hsm_root.dat";

        public DefaultHsmProvider(IDataProtectionProvider provider)
        {
            _protector = provider.CreateProtector("Orion.RootKey.V1");
        }

        public byte[] GetRootKey()
        {
            if (File.Exists(_keyFilePath))
            {
                var encrypted = File.ReadAllBytes(_keyFilePath);
                return _protector.Unprotect(encrypted);
            }

            // Generate new 256-bit key
            var newKey = new byte[32];
            RandomNumberGenerator.Fill(newKey);
            var protectedKey = _protector.Protect(newKey);
            File.WriteAllBytes(_keyFilePath, protectedKey);
            return newKey;
        }
    }
}
