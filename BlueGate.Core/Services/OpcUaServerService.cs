using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Opc.Ua;
using Opc.Ua.Configuration;
using Opc.Ua.Server;
using BlueGate.Core.Models;

namespace BlueGate.Core.Services;

public class OpcUaServerService
{
    private ApplicationInstance? _application;
    private StandardServer? _server;

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

        await _application.CheckApplicationInstanceCertificatesAsync(true, null, CancellationToken.None);

        _server = new StandardServer();
        await _application.StartAsync(_server);

        var endpoint = config.ServerConfiguration.BaseAddresses.FirstOrDefault()
            ?? "opc.tcp://localhost:4840/BlueGate";

        Console.WriteLine($"✅ OPC UA Server started at: {endpoint}");
    }

    private static async Task<ApplicationConfiguration> LoadOrCreateConfigurationAsync(ApplicationInstance application)
    {
        try
        {
            return await application.LoadApplicationConfigurationAsync(silent: false);
        }
        catch (Exception ex) when (ex is FileNotFoundException || ex is ServiceResultException)
        {
            Console.WriteLine("⚠️ No OPC UA configuration file found. Building configuration programmatically...");
            return BuildConfiguration(application.ApplicationName);
        }
    }

    private static ApplicationConfiguration BuildConfiguration(string applicationName)
    {
        var applicationUri = Utils.Format("urn:{0}:{1}", Utils.GetHostName(), applicationName);
        var subjectName = Utils.Format("CN={0}, DC={1}", applicationName, Utils.GetHostName());
        var baseDirectory = AppContext.BaseDirectory;

        var applicationStorePath = Path.GetFullPath(Path.Combine(baseDirectory, "pki", "own"));
        var trustedPeerStorePath = Path.GetFullPath(Path.Combine(baseDirectory, "pki", "trusted"));
        var trustedIssuerStorePath = Path.GetFullPath(Path.Combine(baseDirectory, "pki", "issuers"));
        var rejectedStorePath = Path.GetFullPath(Path.Combine(baseDirectory, "pki", "rejected"));

        Directory.CreateDirectory(applicationStorePath);
        Directory.CreateDirectory(trustedPeerStorePath);
        Directory.CreateDirectory(trustedIssuerStorePath);
        Directory.CreateDirectory(rejectedStorePath);

        var securityConfiguration = new SecurityConfiguration
        {
            ApplicationCertificate = new CertificateIdentifier
            {
                StoreType = CertificateStoreType.Directory,
                StorePath = applicationStorePath,
                SubjectName = subjectName
            },
            TrustedPeerCertificates = new CertificateTrustList
            {
                StoreType = CertificateStoreType.Directory,
                StorePath = trustedPeerStorePath
            },
            TrustedIssuerCertificates = new CertificateTrustList
            {
                StoreType = CertificateStoreType.Directory,
                StorePath = trustedIssuerStorePath
            },
            RejectedCertificateStore = new CertificateTrustList
            {
                StoreType = CertificateStoreType.Directory,
                StorePath = rejectedStorePath
            },
            AutoAcceptUntrustedCertificates = true,
            AddAppCertToTrustedStore = true
        };

        var baseAddress = Utils.Format("opc.tcp://{0}:4840/BlueGate", Utils.GetHostName());

        var serverConfiguration = new ServerConfiguration
        {
            BaseAddresses =
            {
                baseAddress,
                "opc.tcp://localhost:4840/BlueGate"
            },
            SecurityPolicies =
            {
                new ServerSecurityPolicy
                {
                    SecurityMode = MessageSecurityMode.None,
                    SecurityPolicyUri = SecurityPolicies.None
                }
            }
        };

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

        return config;
    }

    public async Task UpdateNodeAsync(OpcUaNode node)
    {
        if (_server == null)
            return;

        try
        {
            var nodeId = new NodeId(node.NodeId);
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
