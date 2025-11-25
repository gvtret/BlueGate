using BlueGate.Core.Configuration;
using BlueGate.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

public class ConversionEngineTests
{
    private readonly FakeDlmsTransport _dlmsTransport;
    private readonly DlmsClientService _dlmsClient;
    private readonly FakeOpcUaServerService _opcUaServer;
    private readonly MappingService _mappingService;
    private readonly ConversionEngine _engine;

    public ConversionEngineTests()
    {
        _dlmsTransport = new FakeDlmsTransport();
        var dlmsOptions = new DlmsClientOptions
        {
            Profiles = new List<ObisMappingProfile>
            {
                new ObisMappingProfile { ObisCode = "1.0.1.8.0.255", OpcNodeId = "ns=2;s=ActiveEnergy", BuiltInType = Opc.Ua.BuiltInType.Double }
            }
        };
        var dlmsOptionsMonitor = Options.Create(dlmsOptions);
        _dlmsClient = new DlmsClientService(_dlmsTransport, dlmsOptionsMonitor, NullLogger<DlmsClientService>.Instance);
        _opcUaServer = new FakeOpcUaServerService();
        _mappingService = new MappingService(dlmsOptionsMonitor);

        _engine = new ConversionEngine(_dlmsClient, _opcUaServer, _mappingService, NullLogger<ConversionEngine>.Instance);
    }

    [Fact]
    public async Task SyncLoopAsync_ShouldSyncData()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        var dlmsData = new Dictionary<string, object>
        {
            { "1.0.1.8.0.255", 123.45 }
        };
        _dlmsTransport.Client.Objects.Add(new Gurux.DLMS.Objects.GXDLMSRegister("1.0.1.8.0.255") { Value = 123.45 });
        _dlmsTransport.IsOpen = true;

        // Act
        var task = _engine.SyncLoopAsync(cts.Token);
        await Task.Delay(100); // Give it time to run once
        cts.Cancel();

        // Assert
        Assert.True(_opcUaServer.Nodes.ContainsKey("ns=2;s=ActiveEnergy"));
        Assert.Equal(123.45, _opcUaServer.Nodes["ns=2;s=ActiveEnergy"]);
    }
}

public class FakeOpcUaServerService : OpcUaServerService
{
    public Dictionary<string, object> Nodes { get; } = new Dictionary<string, object>();

    public FakeOpcUaServerService() : base(null, null, null, null)
    {
    }

    public override Task StartAsync()
    {
        return Task.CompletedTask;
    }

    public override void UpdateNodeValue(string nodeId, object value, Opc.Ua.BuiltInType builtInType)
    {
        Nodes[nodeId] = value;
    }
}
