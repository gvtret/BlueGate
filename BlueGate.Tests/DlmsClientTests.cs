using Xunit;
using BlueGate.Core.Services;

public class DlmsClientTests
{
    [Fact]
    public async Task Should_Read_Data_From_DLMS()
    {
        var client = new DlmsClientService();
        var data = await client.ReadAllAsync();
        Assert.NotNull(data);
    }
}
