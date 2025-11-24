using BlueGate.Core.Configuration;
using BlueGate.Core.Models;

namespace BlueGate.Core.Services;

public interface IDlmsTransport
{
    Task<IEnumerable<CosemObject>> ReadAllAsync(
        DlmsClientOptions options,
        CancellationToken cancellationToken = default);

    Task WriteAsync(
        DlmsClientOptions options,
        string obisCode,
        object value,
        CancellationToken cancellationToken = default);
}
