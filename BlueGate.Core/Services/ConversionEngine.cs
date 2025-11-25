using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace BlueGate.Core.Services
{
    /// <summary>
    /// Orchestrates the bidirectional synchronization between DLMS and OPC UA services.
    /// </summary>
    public class ConversionEngine
    {
        private readonly DlmsClientService _dlmsClient;
        private readonly OpcUaServerService _opcUaServer;
        private readonly MappingService _mappingService;
        private readonly ILogger<ConversionEngine> _logger;

        public ConversionEngine(
            DlmsClientService dlmsClient,
            OpcUaServerService opcUaServer,
            MappingService mappingService,
            ILogger<ConversionEngine> logger)
        {
            _dlmsClient = dlmsClient;
            _opcUaServer = opcUaServer;
            _mappingService = mappingService;
            _logger = logger;
        }

        /// <summary>
        /// Starts the main synchronization loop.
        /// </summary>
        public async Task SyncLoopAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Engine started. Entering main sync loop...");
            await _opcUaServer.StartAsync();

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var dlmsData = await _dlmsClient.ReadAllObjectsAsync();
                    foreach (var profile in _mappingService.GetObisProfiles())
                    {
                        if (dlmsData.TryGetValue(profile.ObisCode, out var value))
                        {
                            _opcUaServer.UpdateNodeValue(profile.OpcNodeId, value, profile.BuiltInType);
                        }
                    }

                    await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "An error occurred during the sync loop.");
                    await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken); // Wait before retrying
                }
            }
        }

        /// <summary>
        /// Gracefully stops the engine.
        /// </summary>
        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Engine is stopping...");
            await _opcUaServer.StopAsync();
        }
    }
}
