using BlueGate.Core.Configuration;
using BlueGate.Core.Models;
using Microsoft.Extensions.Options;
using Opc.Ua;

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
    {
        try
        {
            var parsedNodeId = NodeId.Parse(nodeId);
            return MapToDlms(parsedNodeId);
        }
        catch (Exception ex) when (ex is ServiceResultException or FormatException)
        {
            return null;
        }
    }

    public string? MapToDlms(NodeId nodeId)
    {
        var identifier = nodeId?.Identifier?.ToString();

        if (identifier is null)
            return null;

        foreach (var profile in _profiles)
        {
            try
            {
                var mappedNodeId = NodeId.Parse(profile.OpcNodeId);
                if (mappedNodeId.Identifier?.ToString() == identifier)
                    return profile.ObisCode;
            }
            catch (Exception ex) when (ex is ServiceResultException or FormatException)
            {
                continue;
            }
        }

        return null;
    }

    public IReadOnlyCollection<MappingProfile> GetProfiles() => _profiles.AsReadOnly();
}
