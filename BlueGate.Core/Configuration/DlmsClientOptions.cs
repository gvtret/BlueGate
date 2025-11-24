using Gurux.DLMS.Enums;
using BlueGate.Core.Models;

namespace BlueGate.Core.Configuration;

/// <summary>
/// DLMS transport and authentication settings provided via configuration binding.
/// </summary>
public class DlmsClientOptions
{
    /// <summary>DLMS server host name or IP.</summary>
    public string Host { get; set; } = "192.168.1.10";

    /// <summary>DLMS server TCP port.</summary>
    public int Port { get; set; } = 4059;

    /// <summary>Client address configured on the meter.</summary>
    public int ClientAddress { get; set; } = 16;

    /// <summary>Server address configured on the meter.</summary>
    public int ServerAddress { get; set; } = 1;

    /// <summary>Authentication mechanism to use.</summary>
    public Authentication Authentication { get; set; } = Authentication.None;

    /// <summary>Password or shared secret for authentication.</summary>
    public string? Password { get; set; }

    /// <summary>Interface type for the media (HDLC/TCP-UDP).</summary>
    public InterfaceType InterfaceType { get; set; } = InterfaceType.HDLC;

    /// <summary>How long to wait for replies (milliseconds).</summary>
    public int WaitTime { get; set; } = 5000;

    /// <summary>How many data chunks to read at once.</summary>
    public int ReceiveCount { get; set; } = 1;

    /// <summary>Collection of DLMS to OPC UA mapping profiles to read/write.</summary>
    public List<MappingProfile> Profiles { get; set; } = CreateDefaultProfiles();

    /// <summary>Default mapping profiles used when configuration does not specify any.</summary>
    public static IReadOnlyCollection<MappingProfile> DefaultProfiles { get; } = CreateDefaultProfiles();

    private static List<MappingProfile> CreateDefaultProfiles() => new()
    {
        new()
        {
            ObisCode = "1.0.1.8.0.255",
            OpcNodeId = "ns=2;s=ActiveEnergy",
            ObjectType = ObjectType.Register,
            AttributeIndex = 2
        }
    };
}
