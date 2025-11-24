using System.Linq;
using BlueGate.Core.Configuration;
using BlueGate.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BlueGate.Core.Services;

public class DlmsClientService
{
    private readonly IOptionsMonitor<DlmsClientOptions> _optionsMonitor;
    private readonly IDlmsTransport _transport;
    private readonly MappingService _mappingService;
    private readonly ILogger<DlmsClientService> _logger;

    public DlmsClientService(
        IOptionsMonitor<DlmsClientOptions> options,
        IDlmsTransport transport,
        MappingService mappingService,
        ILogger<DlmsClientService> logger)
    {
        _optionsMonitor = options;
        _transport = transport;
        _mappingService = mappingService;
        _logger = logger;
    }

    public async Task<IEnumerable<CosemObject>> ReadAllAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var options = _optionsMonitor.CurrentValue;
            var profiles = _mappingService.GetProfiles();

            return await _transport.ReadAllAsync(options, profiles, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DLMS read error");
            return Enumerable.Empty<CosemObject>();
        }
    }

    public async Task WriteAsync(string obisCode, object value, CancellationToken cancellationToken = default)
    {
        try
        {
            var options = _optionsMonitor.CurrentValue;
            var profiles = _mappingService.GetProfiles();

            await _transport.WriteAsync(options, obisCode, profiles, value, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DLMS write error");
            throw;
        }
    }
}
