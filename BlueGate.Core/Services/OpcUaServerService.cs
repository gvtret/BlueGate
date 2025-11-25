
using System;
using System.Threading.Tasks;
using BlueGate.Core.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Opc.Ua;
using Opc.Ua.Server;

namespace BlueGate.Core.Services
{
    public class OpcUaServerService
    {
        private readonly ILogger<OpcUaServerService> _logger;
        private readonly IOptionsMonitor<OpcUaOptions> _optionsMonitor;
        private readonly IOptionsMonitor<DlmsClientOptions> _dlmsOptionsMonitor;
        private readonly DlmsClientService _dlmsClient;
        private readonly MappingService _mappingService;
        private readonly ILogger<BlueGateNodeManager> _nodeManagerLogger;
        public StandardServer Server { get; set; }
        private ApplicationConfiguration _config;

        public OpcUaServerService(
            IOptionsMonitor<OpcUaOptions> optionsMonitor,
            IOptionsMonitor<DlmsClientOptions> dlmsOptionsMonitor,
            ILogger<OpcUaServerService> logger,
            ILogger<BlueGateNodeManager> nodeManagerLogger,
            DlmsClientService dlmsClient,
            MappingService mappingService)
        {
            _logger = logger;
            _optionsMonitor = optionsMonitor;
            _dlmsOptionsMonitor = dlmsOptionsMonitor;
            _dlmsClient = dlmsClient;
            _mappingService = mappingService;
            _nodeManagerLogger = nodeManagerLogger;
        }

        private BlueGateNodeManager _nodeManager;

        public virtual async Task StartAsync()
        {
            _logger.LogInformation("Starting OPC UA server...");

            _config = await CreateConfigurationAsync();
            var pkiPath = _config.SecurityConfiguration.ApplicationCertificate.StorePath;
            await _config.Validate(ApplicationType.Server);

            Server = Server ?? new StandardServer
            {
                ApplicationConfiguration = _config
            };

            _nodeManager = new BlueGateNodeManager(Server, _config, _dlmsClient, _mappingService, _nodeManagerLogger, _optionsMonitor.CurrentValue.NamespaceUri);
            Server.AddNodeManager(_nodeManager);

            await Task.Run(() => Server.Start(_config));
            _logger.LogInformation("OPC UA server started on endpoints: {Endpoints}", string.Join(", ", Server.GetEndpoints().Select(e => e.EndpointUrl)));

            _dlmsOptionsMonitor.OnChange(settings =>
            {
                _logger.LogInformation("DLMS mapping configuration changed. Rebuilding OPC UA address space...");
                _nodeManager.RebuildAddressSpace();
            });
        }

        public virtual void UpdateNodeValue(string nodeId, object value, BuiltInType builtInType)
        {
            if (_nodeManager != null)
            {
                _nodeManager.UpdateNodeValue(nodeId, value, builtInType);
            }
        }

        public async Task StopAsync()
        {
            _logger.LogInformation("Stopping OPC UA server...");
            await Task.Run(() => Server.Stop());
        }

        private async Task<ApplicationConfiguration> CreateConfigurationAsync()
        {
            var options = _optionsMonitor.CurrentValue;
            var config = new ApplicationConfiguration
            {
                ApplicationName = "BlueGate.OpcUaServer",
                ApplicationUri = Utils.Format(Guid.NewGuid().ToString()),
                ProductUri = "urn:BlueGate:OpcUaServer",
                ApplicationType = ApplicationType.Server,
                TransportQuotas = new TransportQuotas { OperationTimeout = 15000 },
                ServerConfiguration = new ServerConfiguration
                {
                    BaseAddresses = { options.BaseAddresses.FirstOrDefault() },
                    SecurityPolicies = new Opc.Ua.ServerSecurityPolicyCollection(options.SecurityPolicies.Select(p => new Opc.Ua.ServerSecurityPolicy
                    {
                        SecurityMode = p.SecurityMode,
                        SecurityPolicyUri = p.SecurityPolicy
                    })),
                    UserTokenPolicies = new Opc.Ua.UserTokenPolicyCollection(options.UserTokenPolicies.Select(p => new Opc.Ua.UserTokenPolicy
                    {
                        TokenType = p.TokenType,
                        PolicyId = p.PolicyId,
                        SecurityPolicyUri = p.SecurityPolicy
                    }))
                },
                SecurityConfiguration = new SecurityConfiguration
                {
                    ApplicationCertificate = new CertificateIdentifier
                    {
                        StoreType = options.CertificateStores.ApplicationStore.StoreType,
                        StorePath = options.CertificateStores.ApplicationStore.StorePath,
                        SubjectName = "CN=BlueGate.OpcUaServer"
                    },
                    TrustedPeerCertificates = new CertificateTrustList
                    {
                        StoreType = options.CertificateStores.TrustedPeerStore.StoreType,
                        StorePath = options.CertificateStores.TrustedPeerStore.StorePath,
                    },
                    RejectedCertificateStore = new CertificateTrustList
                    {
                        StoreType = options.CertificateStores.RejectedStore.StoreType,
                        StorePath = options.CertificateStores.RejectedStore.StorePath
                    },
                    AutoAcceptUntrustedCertificates = options.AutoAcceptUntrustedCertificates
                }
            };

            await config.Validate(ApplicationType.Server);
            return config;
        }
    }
}
