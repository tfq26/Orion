using MessagePack;
using Orion.Core.Models;

namespace Orion.Core.Services
{
    public interface ISerializationService
    {
        byte[] Serialize<T>(T value);
        T Deserialize<T>(byte[] data);
    }

    public class SerializationService : ISerializationService
    {
        private readonly MessagePackSerializerOptions _options;

        public SerializationService()
        {
            // Use ContractlessStandardResolver for flexible POCO serialization
            _options = MessagePackSerializerOptions.Standard.WithResolver(MessagePack.Resolvers.ContractlessStandardResolver.Instance);
        }

        public byte[] Serialize<T>(T value)
        {
            return MessagePackSerializer.Serialize(value, _options);
        }

        public T Deserialize<T>(byte[] data)
        {
            return MessagePackSerializer.Deserialize<T>(data, _options);
        }
    }
}
