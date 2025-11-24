using System.Linq;
using BlueGate.Core.Configuration;
using BlueGate.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BlueGate.Core.Services;

public class DlmsClientService
{
    private readonly DlmsClientOptions _options;
    private readonly IDlmsTransport _transport;
    private readonly ILogger<DlmsClientService> _logger;

    public DlmsClientService(
        IOptions<DlmsClientOptions> options,
        IDlmsTransport transport,
        ILogger<DlmsClientService> logger)
    {
        _options = options.Value;
        _transport = transport;
        _logger = logger;
    }

    public async Task<IEnumerable<CosemObject>> ReadAllAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await _transport.ReadAllAsync(_options, cancellationToken).ConfigureAwait(false);
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
            await _transport.WriteAsync(_options, obisCode, value, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DLMS write error");
        }
    }
}
