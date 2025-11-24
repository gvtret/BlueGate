using Gurux.DLMS;

namespace BlueGate.Core.Services
{
    public interface IDlmsAuthenticationService
    {
        Authentication GetAuthentication();
        byte[] GetPassword();
    }
}
