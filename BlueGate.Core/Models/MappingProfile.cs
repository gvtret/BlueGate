using Gurux.DLMS.Enums;
using Opc.Ua;

namespace BlueGate.Core.Models;

public class MappingProfile
{
    public string ObisCode { get; set; } = string.Empty;
    public string OpcNodeId { get; set; } = string.Empty;
    public ObjectType ObjectType { get; set; } = ObjectType.None;
    public int AttributeIndex { get; set; } = 2;

    public BuiltInType? BuiltInType { get; set; }

    public string? DataTypeNodeId { get; set; }

    public object? InitialValue { get; set; }
}
