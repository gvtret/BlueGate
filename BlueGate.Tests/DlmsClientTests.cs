using BlueGate.Core.Configuration;
using BlueGate.Core.Models;
using BlueGate.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

public class DlmsClientTests
{
    [Fact]
    public async Task Should_Read_Data_From_DLMS()
    {
        var expected = new CosemObject
        {
            ObisCode = "1.0.1.8.0.255",
            Value = 123.45,
            Timestamp = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };

        var options = new DlmsClientOptions();
        var optionsMonitor = new TestOptionsMonitor<DlmsClientOptions>(options);
        var mappingService = new MappingService(optionsMonitor, NullLogger<MappingService>.Instance);
        var client = new DlmsClientService(
            optionsMonitor,
            new FakeDlmsTransport(new[] { expected }),
            mappingService,
            NullLogger<DlmsClientService>.Instance);

        var data = await client.ReadAllAsync();

        var cosemObject = Assert.Single(data);
        Assert.Equal(expected.ObisCode, cosemObject.ObisCode);
        Assert.Equal(expected.Value, cosemObject.Value);
        Assert.Equal(expected.Timestamp, cosemObject.Timestamp);
    }
}

internal sealed class FakeDlmsTransport : IDlmsTransport
{
    private readonly IEnumerable<CosemObject> _objects;

    public FakeDlmsTransport(IEnumerable<CosemObject> objects)
    {
        _objects = objects;
    }

    public Task<IEnumerable<CosemObject>> ReadAllAsync(
        DlmsClientOptions options,
        IEnumerable<MappingProfile> profiles,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(_objects);

    public Task WriteAsync(
        DlmsClientOptions options,
        string obisCode,
        IEnumerable<MappingProfile> profiles,
        object value,
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}

internal sealed class TestOptionsMonitor<T> : IOptionsMonitor<T>
{
    public TestOptionsMonitor(T currentValue)
    {
        CurrentValue = currentValue;
    }

    public T CurrentValue { get; private set; }

    public T Get(string? name) => CurrentValue;

    public IDisposable OnChange(Action<T, string?> listener)
    {
        return NullDisposable.Instance;
    }

    private sealed class NullDisposable : IDisposable
    {
        public static readonly NullDisposable Instance = new();

        public void Dispose()
        {
        }
    }
}
