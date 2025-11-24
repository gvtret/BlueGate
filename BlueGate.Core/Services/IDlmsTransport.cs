using BlueGate.Core.Configuration;
using BlueGate.Core.Models;

namespace BlueGate.Core.Services;

public interface IDlmsTransport
{
    Task<IEnumerable<CosemObject>> ReadAllAsync(
        DlmsClientOptions options,
        IEnumerable<MappingProfile> profiles,
        CancellationToken cancellationToken = default);

    Task WriteAsync(
        DlmsClientOptions options,
        string obisCode,
        IEnumerable<MappingProfile> profiles,
        object value,
        CancellationToken cancellationToken = default);
}
