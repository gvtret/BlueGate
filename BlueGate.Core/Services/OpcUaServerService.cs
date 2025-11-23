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

        var config = await _application.LoadApplicationConfigurationAsync(silent: false);
        config.CertificateValidator.CertificateValidation += (s, e) => e.Accept = true;

        // Проверка и генерация сертификата вручную
        var securityConfig = config.SecurityConfiguration;
        if (securityConfig.ApplicationCertificate?.Certificate == null)
        {
            Console.WriteLine("⚙️ Generating new OPC UA application certificate...");
            var cert = CertificateFactory.CreateCertificate(
                securityConfig.ApplicationCertificate.StoreType,
                securityConfig.ApplicationCertificate.StorePath,
                securityConfig.ApplicationCertificate.SubjectName,
                null,
                null,
                securityConfig.ApplicationCertificate.KeySize,
                DateTime.UtcNow - TimeSpan.FromDays(1),
                DateTime.UtcNow.AddYears(5),
                null
            );
            securityConfig.ApplicationCertificate.Certificate = cert;
        }

        _server = new StandardServer();
        await _application.StartAsync(_server);

        Console.WriteLine("✅ OPC UA Server started at: opc.tcp://localhost:4840/BlueGate");
    }

    public async Task UpdateNodeAsync(OpcUaNode node)
    {
        if (_server == null)
            return;

        try
        {
            var session = _server.CurrentInstance.SessionManager.GetSessions().FirstOrDefault();
            if (session == null)
                return;

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

            session.Write(
                null,
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
