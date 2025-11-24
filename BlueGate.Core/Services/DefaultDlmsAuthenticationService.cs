using BlueGate.Core.Configuration;
using Gurux.DLMS;
using Microsoft.Extensions.Options;
using System.Text;

namespace BlueGate.Core.Services
{
    public class DefaultDlmsAuthenticationService : IDlmsAuthenticationService
    {
        private readonly IOptionsMonitor<DlmsClientOptions> _optionsMonitor;

        public DefaultDlmsAuthenticationService(IOptionsMonitor<DlmsClientOptions> optionsMonitor)
        {
            _optionsMonitor = optionsMonitor;
        }

        public Authentication GetAuthentication()
        {
            return _optionsMonitor.CurrentValue.Authentication;
        }

        public byte[] GetPassword()
        {
            return Encoding.ASCII.GetBytes(_optionsMonitor.CurrentValue.Password);
        }
    }
}
