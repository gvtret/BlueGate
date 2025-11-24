using System;
using System.IO;
using System.Net.Sockets;
using System.Threading.Tasks;
using BlueGate.Core.Configuration;
using Gurux.Communication;
using Gurux.DLMS;
using Gurux.DLMS.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BlueGate.Core.Services
{
    public class GuruxDlmsTransport : IDlmsTransport
    {
        private readonly IOptionsMonitor<DlmsClientOptions> _optionsMonitor;
        private readonly ILogger<GuruxDlmsTransport> _logger;
        private readonly IDlmsAuthenticationService _authenticationService;
        private readonly FileInvocationCounter _invocationCounter;
        private GXDLMSClient _client;
        private IGXMedia _media;

        public GuruxDlmsTransport(IOptionsMonitor<DlmsClientOptions> optionsMonitor, ILogger<GuruxDlmsTransport> logger, IDlmsAuthenticationService authenticationService)
        {
            _optionsMonitor = optionsMonitor;
            _logger = logger;
            _authenticationService = authenticationService;
            _invocationCounter = new FileInvocationCounter(optionsMonitor.CurrentValue.InvocationCounterPath);
            _client = new GXDLMSClient();
            UpdateClientSettings();
            _optionsMonitor.OnChange(settings =>
            {
                UpdateClientSettings();
                _invocationCounter = new FileInvocationCounter(settings.InvocationCounterPath);
            });
        }

        private void UpdateClientSettings()
        {
            var options = _optionsMonitor.CurrentValue;
            _client.UseLogicalNameReferencing = true;
            _client.InterfaceType = options.InterfaceType;
            _client.ClientAddress = options.ClientAddress;
            _client.ServerAddress = options.ServerAddress;
            _client.Authentication = _authenticationService.GetAuthentication();
            _client.Password = _authenticationService.GetPassword();
            _client.Security = options.Security;
            _client.SecuritySuite = options.SecuritySuite;
            _client.BlockCipherKey = Gurux.Common.GXCommon.HexToBytes(options.BlockCipherKey);
            _client.AuthenticationKey = Gurux.Common.GXCommon.HexToBytes(options.AuthenticationKey);
            _client.SystemTitle = Gurux.Common.GXCommon.HexToBytes(options.SystemTitle);

            if (options.InvocationCounter.HasValue)
            {
                _client.InvocationCounter = options.InvocationCounter.Value;
            }
            else
            {
                _client.InvocationCounter = _invocationCounter.Get();
            }
        }

        public bool IsOpen => _media?.IsOpen() ?? false;

        public GXDLMSClient Client => _client;

        public async Task ConnectAsync()
        {
            if (IsOpen)
            {
                _logger.LogWarning("Already connected.");
                return;
            }

            var options = _optionsMonitor.CurrentValue;
            _logger.LogInformation("Connecting to DLMS server at {Host}:{Port}...", options.Host, options.Port);
            _media = new GXNet(options.Host, options.Port);
            await Task.Run(() => _media.Open());

            try
            {
                await Task.Run(() =>
                {
                    var data = _client.InitializeConnection();
                    if (data != null && data.Length > 0)
                    {
                        // Handle reply data if necessary
                    }
                });
                _logger.LogInformation("DLMS connection established.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to establish DLMS connection.");
                _media?.Close();
                throw;
            }
        }

        public async Task DisconnectAsync()
        {
            if (!IsOpen)
            {
                _logger.LogWarning("Not connected.");
                return;
            }
            _logger.LogInformation("Disconnecting from DLMS server...");
            _invocationCounter.Set(_client.InvocationCounter);
            await Task.Run(() => _client.Close());
            _media.Close();
        }
        public void Dispose()
        {
            _media?.Close();
            _media?.Dispose();
        }
    }
}
