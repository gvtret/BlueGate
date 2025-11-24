using BlueGate.Core.Configuration;
using BlueGate.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Opc.Ua;
using Opc.Ua.Server;
using System.Threading.Tasks;
using Xunit;

public class OpcUaServerServiceTests
{
    private readonly OpcUaServerService _service;
    private readonly FakeDlmsTransport _dlmsTransport;
    private readonly DlmsClientService _dlmsClient;
    private readonly MappingService _mappingService;
    private readonly FakeStandardServer _standardServer;
    private readonly FakeBlueGateNodeManager _nodeManager;
    private readonly TestOptionsMonitor<DlmsClientOptions> _dlmsOptionsMonitor;

    public OpcUaServerServiceTests()
    {
        var options = new OpcUaOptions();
        var optionsMonitor = new TestOptionsMonitor<OpcUaOptions>(options);

        _dlmsTransport = new FakeDlmsTransport();
        var dlmsOptions = new DlmsClientOptions();
        _dlmsOptionsMonitor = new TestOptionsMonitor<DlmsClientOptions>(dlmsOptions);
        var dlmsAuthService = new DefaultDlmsAuthenticationService(_dlmsOptionsMonitor);
        _dlmsClient = new DlmsClientService(new GuruxDlmsTransport(_dlmsOptionsMonitor, NullLogger<GuruxDlmsTransport>.Instance, dlmsAuthService), _dlmsOptionsMonitor, NullLogger<DlmsClientService>.Instance);
        _mappingService = new MappingService(_dlmsOptionsMonitor);
        _standardServer = new FakeStandardServer();
        _nodeManager = new FakeBlueGateNodeManager(_standardServer, new ApplicationConfiguration(), _dlmsClient, _mappingService, NullLogger<BlueGateNodeManager>.Instance, options.NamespaceUri);

        _service = new OpcUaServerService(optionsMonitor, _dlmsOptionsMonitor, NullLogger<OpcUaServerService>.Instance, NullLogger<BlueGateNodeManager>.Instance, _dlmsClient, _mappingService)
        {
            Server = _standardServer
        };
    }

    [Fact]
    public async Task StartAsync_ShouldStartServer()
    {
        // Arrange

        // Act
        await _service.StartAsync();

        // Assert
        Assert.True(_standardServer.IsStarted);
    }

    [Fact]
    public async Task OnDlmsOptionsChanged_ShouldRebuildAddressSpace()
    {
        // Arrange
        await _service.StartAsync();

        // Act
        _dlmsOptionsMonitor.Change(new DlmsClientOptions());

        // Assert
        Assert.True(_nodeManager.IsRebuilt);
    }
}

public class FakeStandardServer : StandardServer
{
    public bool IsStarted { get; private set; }

    public override void Start(ApplicationConfiguration configuration)
    {
        IsStarted = true;
    }
}

public class FakeBlueGateNodeManager : BlueGateNodeManager
{
    public bool IsRebuilt { get; private set; }

    public FakeBlueGateNodeManager(IServerInternal server, ApplicationConfiguration configuration, DlmsClientService dlmsClient, MappingService mappingService, ILogger<BlueGateNodeManager> logger, string namespaceUri) : base(server, configuration, dlmsClient, mappingService, logger, namespaceUri)
    {
    }

    public override void RebuildAddressSpace()
    {
        IsRebuilt = true;
    }
}
