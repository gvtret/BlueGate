namespace BlueGate.Core.Models;

public class CosemObject
{
    public string ObisCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public object? Value { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
