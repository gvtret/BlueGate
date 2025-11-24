using Serilog;
using Serilog.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using BlueGate.Core.Configuration;
using BlueGate.Core.Services;

var builder = Host.CreateApplicationBuilder(args);
builder.Configuration.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);

// Настройка Serilog
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console() // теперь доступен
    .CreateLogger();

builder.Services.AddOptions<DlmsClientOptions>()
    .Bind(builder.Configuration.GetSection("DlmsClient"))
    .ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<DlmsClientOptions>, DlmsClientOptionsValidator>();
builder.Services.AddOptions<OpcUaOptions>()
    .Bind(builder.Configuration.GetSection("OpcUa"))
    .ValidateOnStart();
builder.Services.AddSingleton<IDlmsAuthenticationService, DefaultDlmsAuthenticationService>();
builder.Services.AddSingleton<IDlmsTransport, GuruxDlmsTransport>();
builder.Services.AddSingleton<DlmsClientService>();
builder.Services.AddSingleton<OpcUaServerService>(provider =>
{
    var options = provider.GetRequiredService<IOptionsMonitor<OpcUaOptions>>();
    var dlmsOptions = provider.GetRequiredService<IOptionsMonitor<DlmsClientOptions>>();
    var logger = provider.GetRequiredService<ILogger<OpcUaServerService>>();
    var nodeManagerLogger = provider.GetRequiredService<ILogger<BlueGateNodeManager>>();
    var dlmsClient = provider.GetRequiredService<DlmsClientService>();
    var mappingService = provider.GetRequiredService<MappingService>();
    return new OpcUaServerService(options, dlmsOptions, logger, nodeManagerLogger, dlmsClient, mappingService);
});
builder.Services.AddSingleton<MappingService>();
builder.Services.AddHostedService<BlueGateWorker>();

builder.Logging.ClearProviders();
builder.Logging.AddSerilog();

var app = builder.Build();
await app.RunAsync();

public class BlueGateWorker : BackgroundService
{
    private readonly ConversionEngine _engine;
    private readonly ILogger<BlueGateWorker> _logger;

    public BlueGateWorker(
        DlmsClientService dlms,
        OpcUaServerService opcua,
        MappingService map,
        ILogger<BlueGateWorker> logger)
    {
        _engine = new ConversionEngine(dlms, opcua, map);
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("🔵 BlueGate gateway service started...");
        await _engine.SyncLoopAsync(stoppingToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("🔻 BlueGate gateway service stopping...");
        await _engine.StopAsync(cancellationToken);
        await base.StopAsync(cancellationToken);
    }
}
