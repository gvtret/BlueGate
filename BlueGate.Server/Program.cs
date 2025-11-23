using Serilog;
using Serilog.Events;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using BlueGate.Core.Services;

var builder = Host.CreateApplicationBuilder(args);

// Настройка Serilog
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console() // теперь доступен
    .CreateLogger();

builder.Services.AddSingleton<DlmsClientService>();
builder.Services.AddSingleton<OpcUaServerService>();
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
}
