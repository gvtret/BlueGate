using BlueGate.Core.Models;

namespace BlueGate.Core.Services;

public class MappingService
{
    private readonly List<MappingProfile> _profiles;

    public MappingService()
    {
        _profiles = new List<MappingProfile>
        {
            new() { ObisCode = "1.0.1.8.0.255", OpcNodeId = "ns=2;s=ActiveEnergy" }
        };
    }

    public string? MapToOpcUa(string obisCode)
        => _profiles.FirstOrDefault(m => m.ObisCode == obisCode)?.OpcNodeId;

    public string? MapToDlms(string nodeId)
        => _profiles.FirstOrDefault(m => m.OpcNodeId == nodeId)?.ObisCode;
}
