using BlueGate.Core.Models;
using Gurux.DLMS;
using Gurux.DLMS.Enums;
using Gurux.DLMS.Objects;
using Gurux.Net;

namespace BlueGate.Core.Services;

public class DlmsClientService
{
    private readonly GXDLMSClient _client;
    private readonly GXNet _media;

    public DlmsClientService()
    {
        _media = new GXNet(NetworkType.Tcp, "192.168.1.10", 4059);

        _client = new GXDLMSClient(
            useLogicalNameReferencing: true,
            clientAddress: 16,
            serverAddress: 1,
            authentication: Authentication.None,
            password: null,
            interfaceType: InterfaceType.HDLC
        );
    }

    public async System.Threading.Tasks.Task<IEnumerable<CosemObject>> ReadAllAsync()
    {
        var results = new List<CosemObject>();

        try
        {
            _media.Open();

            // В новых версиях — просто создаём объект напрямую:
            var register = new GXDLMSRegister("1.0.1.8.0.255"); // пример OBIS для активной энергии

            var value = _client.Read(register, 2);

            results.Add(new CosemObject
            {
                ObisCode = register.LogicalName,
                Value = value,
                Timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ DLMS read error: {ex.Message}");
        }
        finally
        {
            _media.Close();
        }

        return await System.Threading.Tasks.Task.FromResult(results);
    }

    public async System.Threading.Tasks.Task WriteAsync(string obisCode, object value)
    {
        try
        {
            _media.Open();

            var register = new GXDLMSRegister(obisCode);
#pragma warning disable CS0618 // GXDLMSClient.Write obsolete in current package version
            _client.Write(register, value, DataType.None, ObjectType.Register, 2);
#pragma warning restore CS0618
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ DLMS write error: {ex.Message}");
        }
        finally
        {
            _media.Close();
        }

        await System.Threading.Tasks.Task.CompletedTask;
    }
}
