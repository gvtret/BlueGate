
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BlueGate.Core.Configuration;
using Gurux.DLMS.Objects;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Gurux.DLMS;
using System.ComponentModel;

namespace BlueGate.Core.Services
{
    public class DlmsClientService
    {
        private readonly IDlmsTransport _transport;
        private readonly ILogger<DlmsClientService> _logger;
        private readonly IOptionsMonitor<DlmsClientOptions> _optionsMonitor;

        public DlmsClientService(
            IDlmsTransport transport,
            IOptionsMonitor<DlmsClientOptions> optionsMonitor,
            ILogger<DlmsClientService> logger)
        {
            _transport = transport;
            _logger = logger;
            _optionsMonitor = optionsMonitor;
        }

        public async Task ReadAllAsync()
        {
            var client = _transport.Client;
            if (client.IsConnected)
            {
                var associationObjects = client.Objects.GetObjects(ObjectType.Association);
                if (associationObjects.Count == 0)
                {
                    throw new InvalidOperationException("DLMS association view not found.");
                }

                var association = (GXDLMSAssociation)associationObjects[0];
                await Task.Run(() => client.ReadObjects(false, association.ObjectList));
                _logger.LogInformation("Finished reading all objects from the association view.");
            }
        }

        public async Task<Dictionary<string, object>> ReadAllObjectsAsync()
        {
            if (!_transport.IsOpen)
            {
                await _transport.ConnectAsync();
            }

            await ReadAllAsync(); // Ensure all objects are populated

            var results = new Dictionary<string, object>();
            var client = _transport.Client;
            foreach (var profile in _optionsMonitor.CurrentValue.Profiles)
            {
                var target = client.Objects.FindByLN(profile.ObjectType, profile.ObisCode);
                if (target != null)
                {
                    var value = target.GetValue(profile.AttributeIndex);
                    results[profile.ObisCode] = value;
                    _logger.LogInformation("Read value '{Value}' from OBIS {ObisCode}", value, profile.ObisCode);
                }
            }
            return results;
        }

        public async Task WriteObjectAsync(ObisMappingProfile profile, object value)
        {
            if (!_transport.IsOpen)
            {
                await _transport.ConnectAsync();
            }

            var client = _transport.Client;
            var target = client.Objects.FindByLN(profile.ObjectType, profile.ObisCode);
            if (target == null)
            {
                throw new InvalidOperationException($"Object with OBIS code {profile.ObisCode} not found in the association view.");
            }

            await Task.Run(() => client.Write(target, profile.AttributeIndex, value));
            _logger.LogInformation("Wrote value '{Value}' to OBIS {ObisCode}", value, profile.ObisCode);
        }
    }
}
