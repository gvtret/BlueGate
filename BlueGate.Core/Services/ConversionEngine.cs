namespace BlueGate.Core.Services;

public class ConversionEngine
{
    private readonly DlmsClientService _dlms;
    private readonly OpcUaServerService _opcua;
    private readonly MappingService _mapper;

    public ConversionEngine(DlmsClientService dlms, OpcUaServerService opcua, MappingService mapper)
    {
        _dlms = dlms;
        _opcua = opcua;
        _mapper = mapper;
    }

    public async Task SyncLoopAsync(CancellationToken token)
    {
        await _opcua.StartAsync();

        try
        {
            while (!token.IsCancellationRequested)
            {
                var cosemObjects = await _dlms.ReadAllAsync(token);

                foreach (var obj in cosemObjects)
                {
                    var nodeId = _mapper.MapToOpcUa(obj.ObisCode);
                    if (nodeId is not null)
                        await _opcua.UpdateNodeAsync(new Models.OpcUaNode
                        {
                            NodeId = nodeId,
                            Value = obj.Value,
                            LastUpdate = DateTime.UtcNow
                        });
                }

                await Task.Delay(TimeSpan.FromSeconds(10), token);
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
        finally
        {
            await StopAsync(CancellationToken.None);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken = default) => _opcua.StopAsync(cancellationToken);
}
