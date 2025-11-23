namespace BlueGate.Core.Models;

public class OpcUaNode
{
    public string NodeId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public object? Value { get; set; }
    public DateTime LastUpdate { get; set; } = DateTime.UtcNow;
}
