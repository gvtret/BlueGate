using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Opc.Ua;
using Opc.Ua.Configuration;
using Opc.Ua.Server;
using BlueGate.Core.Models;
using BlueGate.Core.Configuration;

namespace BlueGate.Core.Services;

public class OpcUaServerService : IAsyncDisposable
{
    private ApplicationInstance? _application;
    private BlueGateServer? _server;
    private readonly MappingService _mappingService;
    private readonly DlmsClientService _dlmsClientService;
    private readonly ILoggerFactory _loggerFactory;
    private readonly OpcUaOptions _opcUaOptions;
    private ushort? _namespaceIndex;

    public OpcUaServerService(
        MappingService mappingService,
        DlmsClientService dlmsClientService,
        ILoggerFactory loggerFactory,
        IOptions<OpcUaOptions> opcUaOptions)
    {
        _mappingService = mappingService;
        _dlmsClientService = dlmsClientService;
        _loggerFactory = loggerFactory;
        _opcUaOptions = opcUaOptions.Value;
    }

    public async Task StartAsync()
    {
        _application = new ApplicationInstance
        {
            ApplicationName = "BlueGate OPC UA Server",
            ApplicationType = ApplicationType.Server,
            ConfigSectionName = "BlueGateOPCUA"
        };

        var config = await LoadOrCreateConfigurationAsync(_application);
        _application.ApplicationConfiguration = config;

        await config.ValidateAsync(ApplicationType.Server);

        config.CertificateValidator.CertificateValidation += (s, e) =>
        {
            if (config.SecurityConfiguration.AutoAcceptUntrustedCertificates)
            {
                e.Accept = true;
            }
        };

        var minimumKeySize = GetMinimumKeySize();
        await _application.CheckApplicationInstanceCertificatesAsync(false, minimumKeySize, CancellationToken.None);

        _server = new BlueGateServer(_mappingService, _dlmsClientService, _loggerFactory);
        await _application.StartAsync(_server);

        _namespaceIndex = _server.NodeManager?.ServerNamespaceIndex;

        var endpoint = config.ServerConfiguration.BaseAddresses.FirstOrDefault()
            ?? "opc.tcp://localhost:4840/BlueGate";

        Console.WriteLine($"✅ OPC UA Server started at: {endpoint}");
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (_application != null)
        {
            var stopAsyncMethod = _application
                .GetType()
                .GetMethod("StopAsync", new[] { typeof(CancellationToken) });

            if (stopAsyncMethod?.Invoke(_application, new object[] { cancellationToken }) is Task stopTask)
            {
                await stopTask.ConfigureAwait(false);
            }
            else
            {
                _application.Stop();
            }
        }

        if (_server is IAsyncDisposable asyncDisposableServer)
        {
            await asyncDisposableServer.DisposeAsync().ConfigureAwait(false);
        }
        else
        {
            (_server as IDisposable)?.Dispose();
        }

        _namespaceIndex = null;
        _server = null;
        _application = null;
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync().ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }

    private Task<ApplicationConfiguration> LoadOrCreateConfigurationAsync(ApplicationInstance application)
    {
        Console.WriteLine("⚙️ Building OPC UA configuration from application settings...");
        return Task.FromResult(BuildConfiguration(application.ApplicationName));
    }

    private ApplicationConfiguration BuildConfiguration(string applicationName)
    {
        var applicationUri = Utils.Format("urn:{0}:{1}", Utils.GetHostName(), applicationName);
        var subjectName = _opcUaOptions.ApplicationSubjectName
            ?? Utils.Format("CN={0}, DC={1}", applicationName, Utils.GetHostName());
        var baseDirectory = AppContext.BaseDirectory;

        var applicationStorePath = ResolveStorePath(_opcUaOptions.CertificateStores.ApplicationStore.StorePath, baseDirectory, "pki/own");
        var trustedPeerStorePath = ResolveStorePath(_opcUaOptions.CertificateStores.TrustedPeerStore.StorePath, baseDirectory, "pki/trusted");
        var trustedIssuerStorePath = ResolveStorePath(_opcUaOptions.CertificateStores.TrustedIssuerStore.StorePath, baseDirectory, "pki/issuers");
        var rejectedStorePath = ResolveStorePath(_opcUaOptions.CertificateStores.RejectedStore.StorePath, baseDirectory, "pki/rejected");

        Directory.CreateDirectory(applicationStorePath);
        Directory.CreateDirectory(trustedPeerStorePath);
        Directory.CreateDirectory(trustedIssuerStorePath);
        Directory.CreateDirectory(rejectedStorePath);

        var securityConfiguration = new SecurityConfiguration
        {
            ApplicationCertificate = new CertificateIdentifier
            {
                StoreType = _opcUaOptions.CertificateStores.ApplicationStore.StoreType,
                StorePath = applicationStorePath,
                SubjectName = subjectName
            },
            TrustedPeerCertificates = new CertificateTrustList
            {
                StoreType = _opcUaOptions.CertificateStores.TrustedPeerStore.StoreType,
                StorePath = trustedPeerStorePath
            },
            TrustedIssuerCertificates = new CertificateTrustList
            {
                StoreType = _opcUaOptions.CertificateStores.TrustedIssuerStore.StoreType,
                StorePath = trustedIssuerStorePath
            },
            RejectedCertificateStore = new CertificateTrustList
            {
                StoreType = _opcUaOptions.CertificateStores.RejectedStore.StoreType,
                StorePath = rejectedStorePath
            },
            AutoAcceptUntrustedCertificates = _opcUaOptions.AutoAcceptUntrustedCertificates,
            AddAppCertToTrustedStore = _opcUaOptions.AddApplicationCertificateToTrustedStore,
            MinimumCertificateKeySize = GetMinimumKeySize()
        };

        var serverConfiguration = BuildServerConfiguration();

        var config = new ApplicationConfiguration
        {
            ApplicationName = applicationName,
            ApplicationType = ApplicationType.Server,
            ApplicationUri = applicationUri,
            SecurityConfiguration = securityConfiguration,
            ServerConfiguration = serverConfiguration,
            CertificateValidator = new CertificateValidator(),
            TransportQuotas = new TransportQuotas
            {
                OperationTimeout = 15000
            },
            ClientConfiguration = new ClientConfiguration
            {
                DefaultSessionTimeout = 60000
            }
        };

        config.CertificateValidator.UpdateAsync(config)
            .GetAwaiter()
            .GetResult();

        return config;
    }

    private ServerConfiguration BuildServerConfiguration()
    {
        var baseAddress = Utils.Format("opc.tcp://{0}:4840/BlueGate", Utils.GetHostName());
        var baseAddresses = _opcUaOptions.BaseAddresses?.Count > 0
            ? _opcUaOptions.BaseAddresses
            : new[] { baseAddress, "opc.tcp://localhost:4840/BlueGate" };

        var configuredPolicies = _opcUaOptions.SecurityPolicies?.Count > 0
            ? _opcUaOptions.SecurityPolicies.ToList()
            : new List<OpcUaSecurityPolicyOptions>
            {
                new()
                {
                    SecurityMode = MessageSecurityMode.SignAndEncrypt,
                    SecurityPolicy = SecurityPolicies.Basic256Sha256
                },
                new()
                {
                    SecurityMode = MessageSecurityMode.Sign,
                    SecurityPolicy = SecurityPolicies.Basic128Rsa15
                }
            };

        if (configuredPolicies.All(p => p.SecurityMode == MessageSecurityMode.None))
        {
            configuredPolicies.Add(new OpcUaSecurityPolicyOptions
            {
                SecurityMode = MessageSecurityMode.SignAndEncrypt,
                SecurityPolicy = SecurityPolicies.Basic256Sha256
            });
        }

        var securityPolicies = new ServerSecurityPolicyCollection(
            configuredPolicies.Select(p => new ServerSecurityPolicy
            {
                SecurityMode = p.SecurityMode,
                SecurityPolicyUri = string.IsNullOrWhiteSpace(p.SecurityPolicy)
                    ? SecurityPolicies.Basic256Sha256
                    : p.SecurityPolicy
            }));

        var userTokenPolicies = BuildUserTokenPolicies();

        var serverConfiguration = new ServerConfiguration();
        foreach (var address in baseAddresses)
        {
            serverConfiguration.BaseAddresses.Add(address);
        }

        foreach (var policy in securityPolicies)
        {
            serverConfiguration.SecurityPolicies.Add(policy);
        }

        foreach (var tokenPolicy in userTokenPolicies)
        {
            serverConfiguration.UserTokenPolicies.Add(tokenPolicy);
        }

        return serverConfiguration;
    }

    private IEnumerable<UserTokenPolicy> BuildUserTokenPolicies()
    {
        var configuredPolicies = _opcUaOptions.UserTokenPolicies?.Count > 0
            ? _opcUaOptions.UserTokenPolicies
            : new List<UserTokenPolicyOptions>
            {
                new()
                {
                    PolicyId = "anonymous",
                    TokenType = UserTokenType.Anonymous,
                    SecurityPolicy = SecurityPolicies.None
                },
                new()
                {
                    PolicyId = "username",
                    TokenType = UserTokenType.UserName,
                    SecurityPolicy = SecurityPolicies.Basic256Sha256
                }
            };

        return configuredPolicies.Select(p => new UserTokenPolicy
        {
            PolicyId = string.IsNullOrWhiteSpace(p.PolicyId) ? p.TokenType.ToString() : p.PolicyId,
            TokenType = p.TokenType,
            SecurityPolicyUri = string.IsNullOrWhiteSpace(p.SecurityPolicy)
                ? SecurityPolicies.None
                : p.SecurityPolicy
        });
    }

    private static string ResolveStorePath(string path, string baseDirectory, string defaultRelative)
    {
        var storePath = string.IsNullOrWhiteSpace(path) ? defaultRelative : path;
        return Path.IsPathRooted(storePath)
            ? storePath
            : Path.GetFullPath(Path.Combine(baseDirectory, storePath));
    }

    private ushort GetMinimumKeySize()
    {
        if (_opcUaOptions.MinimumCertificateKeySize.HasValue)
        {
            return (ushort)Math.Min(ushort.MaxValue, _opcUaOptions.MinimumCertificateKeySize.Value);
        }

        var policies = _opcUaOptions.SecurityPolicies?.Count > 0
            ? _opcUaOptions.SecurityPolicies
            : new List<OpcUaSecurityPolicyOptions>
            {
                new()
                {
                    SecurityMode = MessageSecurityMode.SignAndEncrypt,
                    SecurityPolicy = SecurityPolicies.Basic256Sha256
                },
                new()
                {
                    SecurityMode = MessageSecurityMode.Sign,
                    SecurityPolicy = SecurityPolicies.Basic128Rsa15
                }
            };

        return policies.Any(p => p.SecurityPolicy == SecurityPolicies.Basic256Sha256)
            ? (ushort)2048
            : (ushort)1024;
    }

    public async Task UpdateNodeAsync(OpcUaNode node)
    {
        if (_server == null)
            return;

        try
        {
            var parsedNodeId = NodeId.Parse(node.NodeId);
            var namespaceIndex = _namespaceIndex ?? parsedNodeId.NamespaceIndex;
            var nodeId = parsedNodeId.NamespaceIndex == namespaceIndex
                ? parsedNodeId
                : new NodeId(parsedNodeId.Identifier, namespaceIndex);
            var value = new DataValue(new Variant(node.Value))
            {
                ServerTimestamp = DateTime.UtcNow
            };

            var writeValues = new WriteValueCollection
            {
                new WriteValue
                {
                    NodeId = nodeId,
                    AttributeId = Attributes.Value,
                    Value = value
                }
            };

            var serverInternal = _server.CurrentInstance;
            var operationContext = new OperationContext(new RequestHeader(), RequestType.Write, (ISession?)null);

            serverInternal.NodeManager.Write(
                operationContext,
                writeValues,
                out var results,
                out var diag
            );

            await Task.CompletedTask;

            if (results.Any(r => StatusCode.IsBad(r)))
                Console.WriteLine($"⚠️ OPC UA write failed for node {node.NodeId}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ OPC UA write error: {ex.Message}");
        }
    }
}
