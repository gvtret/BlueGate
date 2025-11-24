using BlueGate.Core.Configuration;
using BlueGate.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

public class DlmsClientTests
{
    [Fact]
    public async Task Should_Read_Data_From_DLMS()
    {
        var options = Options.Create(new DlmsClientOptions());
        var client = new DlmsClientService(options, NullLogger<DlmsClientService>.Instance);

        var data = await client.ReadAllAsync();
        Assert.NotNull(data);
    }
}
