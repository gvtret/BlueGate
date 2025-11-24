using BlueGate.Core.Configuration;
using BlueGate.Core.Services;
using Gurux.DLMS;
using Gurux.DLMS.Client;
using Gurux.DLMS.Objects;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

public class DlmsClientServiceTests
{
    private readonly FakeDlmsTransport _transport;
    private readonly DlmsClientService _service;

    public DlmsClientServiceTests()
    {
        _transport = new FakeDlmsTransport();
        var options = new DlmsClientOptions
        {
            Profiles = new List<ObisMappingProfile>
            {
                new ObisMappingProfile { ObisCode = "1.0.1.8.0.255", AttributeIndex = 2, ObjectType = ObjectType.Register }
            }
        };
        var optionsMonitor = new TestOptionsMonitor<DlmsClientOptions>(options);

        _service = new DlmsClientService(_transport, optionsMonitor, NullLogger<DlmsClientService>.Instance);
    }

    [Fact]
    public async Task ReadAllObjectsAsync_ShouldReturnCorrectValues()
    {
        // Arrange
        var register = new GXDLMSRegister("1.0.1.8.0.255");
        register.Value = 123.45;
        _transport.Client.Objects.Add(register);
        _transport.IsOpen = true;

        // Act
        var result = await _service.ReadAllObjectsAsync();

        // Assert
        Assert.NotNull(result);
        Assert.True(result.ContainsKey("1.0.1.8.0.255"));
        Assert.Equal(123.45, result["1.0.1.8.0.255"]);
    }

    [Fact]
    public async Task WriteObjectAsync_ShouldWriteValue()
    {
        // Arrange
        var register = new GXDLMSRegister("1.0.1.8.0.255");
        _transport.Client.Objects.Add(register);
        _transport.IsOpen = true;
        var profile = new ObisMappingProfile { ObisCode = "1.0.1.8.0.255", AttributeIndex = 2, ObjectType = ObjectType.Register };

        // Act
        await _service.WriteObjectAsync(profile, 543.21);

        // Assert
        Assert.Equal(543.21, register.Value);
    }
}

public class FakeDlmsTransport : IDlmsTransport
{
    public GXDLMSClient Client { get; } = new GXDLMSClient();
    public bool IsOpen { get; set; }

    public Task ConnectAsync()
    {
        IsOpen = true;
        return Task.CompletedTask;
    }

    public Task DisconnectAsync()
    {
        IsOpen = false;
        return Task.CompletedTask;
    }

    public void Dispose()
    {
    }
}

public sealed class TestOptionsMonitor<T> : IOptionsMonitor<T>
{
    private Action<T, string> _listener;

    public TestOptionsMonitor(T currentValue)
    {
        CurrentValue = currentValue;
    }

    public T CurrentValue { get; private set; }

    public T Get(string name) => CurrentValue;

    public IDisposable OnChange(Action<T, string> listener)
    {
        _listener = listener;
        return new NullDisposable();
    }

    public void Change(T value)
    {
        CurrentValue = value;
        _listener?.Invoke(CurrentValue, null);
    }

    private sealed class NullDisposable : IDisposable
    {
        public void Dispose()
        {
        }
    }
}
