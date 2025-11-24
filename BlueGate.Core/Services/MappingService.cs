using BlueGate.Core.Configuration;
using BlueGate.Core.Models;
using Microsoft.Extensions.Options;

namespace BlueGate.Core.Services;

public class MappingService
{
    private readonly List<MappingProfile> _profiles;

    public MappingService(IOptions<DlmsClientOptions> options)
    {
        var configuredProfiles = options.Value.Profiles;
        _profiles = configuredProfiles is { Count: > 0 }
            ? new List<MappingProfile>(configuredProfiles)
            : new List<MappingProfile>();
    }

    public string? MapToOpcUa(string obisCode)
        => _profiles.FirstOrDefault(m => m.ObisCode == obisCode)?.OpcNodeId;

    public string? MapToDlms(string nodeId)
        => _profiles.FirstOrDefault(m => m.OpcNodeId == nodeId)?.ObisCode;

    public IReadOnlyCollection<MappingProfile> GetProfiles() => _profiles.AsReadOnly();
}
