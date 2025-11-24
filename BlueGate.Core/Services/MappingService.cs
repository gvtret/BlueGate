
using BlueGate.Core.Configuration;
using Microsoft.Extensions.Options;
using System.Collections.Generic;

namespace BlueGate.Core.Services
{
    public class MappingService
    {
        private readonly IOptionsMonitor<DlmsClientOptions> _optionsMonitor;

        public MappingService(IOptionsMonitor<DlmsClientOptions> optionsMonitor)
        {
            _optionsMonitor = optionsMonitor;
        }

        public IEnumerable<ObisMappingProfile> GetObisProfiles()
        {
            return _optionsMonitor.CurrentValue.Profiles;
        }
    }
}
