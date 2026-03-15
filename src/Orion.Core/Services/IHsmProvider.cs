namespace Orion.Core.Services
{
    public interface IHsmProvider
    {
        byte[] GetRootKey();
    }
}
