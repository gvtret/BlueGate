using System.IO;
using BlueGate.Core.Configuration;
using BlueGate.Core.Models;
using Gurux.Common;
using Gurux.DLMS;
using Gurux.DLMS.Enums;
using Gurux.DLMS.Objects;
using Gurux.Net;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Task = System.Threading.Tasks.Task;

namespace BlueGate.Core.Services;

public class DlmsClientService
{
    private readonly DlmsClientOptions _options;
    private readonly ILogger<DlmsClientService> _logger;

    public DlmsClientService(IOptions<DlmsClientOptions> options, ILogger<DlmsClientService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<IEnumerable<CosemObject>> ReadAllAsync(CancellationToken cancellationToken = default)
    {
        var results = new List<CosemObject>();

        await using var media = new AsyncDisposableMedia(CreateMedia());
        var client = CreateClient();

        try
        {
            await OpenAsync(media.Media, cancellationToken).ConfigureAwait(false);
            await EstablishAssociationAsync(client, media.Media, cancellationToken).ConfigureAwait(false);

            var register = new GXDLMSRegister("1.0.1.8.0.255");
            var value = await ReadValueAsync(client, media.Media, register, 2, cancellationToken).ConfigureAwait(false);

            results.Add(new CosemObject
            {
                ObisCode = register.LogicalName,
                Value = value,
                Timestamp = DateTime.UtcNow
            });

            await ReleaseAssociationAsync(client, media.Media, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DLMS read error");
        }
        finally
        {
            await CloseAsync(media.Media, cancellationToken).ConfigureAwait(false);
        }

        return results;
    }

    public async Task WriteAsync(string obisCode, object value, CancellationToken cancellationToken = default)
    {
        await using var media = new AsyncDisposableMedia(CreateMedia());
        var client = CreateClient();

        try
        {
            await OpenAsync(media.Media, cancellationToken).ConfigureAwait(false);
            await EstablishAssociationAsync(client, media.Media, cancellationToken).ConfigureAwait(false);

            var register = new GXDLMSRegister(obisCode);
#pragma warning disable CS0618
            var writeFrames = client.Write(register, value, DataType.None, ObjectType.Register, 2);
#pragma warning restore CS0618
            await SendRequestFramesAsync(client, media.Media, writeFrames, cancellationToken).ConfigureAwait(false);

            await ReleaseAssociationAsync(client, media.Media, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DLMS write error");
        }
        finally
        {
            await CloseAsync(media.Media, cancellationToken).ConfigureAwait(false);
        }
    }

    private GXDLMSClient CreateClient() => new(
        useLogicalNameReferencing: true,
        clientAddress: _options.ClientAddress,
        serverAddress: _options.ServerAddress,
        authentication: _options.Authentication,
        password: _options.Password,
        interfaceType: _options.InterfaceType
    );

    private GXNet CreateMedia() => new(NetworkType.Tcp, _options.Host, _options.Port)
    {
        WaitTime = _options.WaitTime
    };

    private static Task OpenAsync(GXNet media, CancellationToken cancellationToken) =>
        Task.Run(media.Open, cancellationToken);

    private static Task CloseAsync(GXNet media, CancellationToken cancellationToken) =>
        Task.Run(media.Close, cancellationToken);

    private async Task EstablishAssociationAsync(GXDLMSClient client, GXNet media, CancellationToken cancellationToken)
    {
        var reply = new GXReplyData();

        await SendRequestFrameAsync(client, media, client.SNRMRequest(false), reply, cancellationToken).ConfigureAwait(false);
        if (reply.Data.Size != 0)
        {
            client.ParseUAResponse(reply.Data);
        }

        reply.Clear();
        await SendRequestFramesAsync(client, media, client.AARQRequest(), cancellationToken, reply).ConfigureAwait(false);
        client.ParseAAREResponse(reply.Data);

        reply.Clear();
        var associationRequest = client.GetApplicationAssociationRequest();
        if (associationRequest is not null && associationRequest.Length > 0)
        {
            await SendRequestFramesAsync(client, media, associationRequest, cancellationToken, reply).ConfigureAwait(false);
            client.ParseApplicationAssociationResponse(reply.Data);
        }
    }

    private async Task ReleaseAssociationAsync(GXDLMSClient client, GXNet media, CancellationToken cancellationToken)
    {
        var reply = new GXReplyData();
        await SendRequestFramesAsync(client, media, client.ReleaseRequest(), cancellationToken, reply).ConfigureAwait(false);
        if (reply.Data.Size != 0)
        {
            client.ParseRelease(reply.Data);
        }

        var disconnect = client.DisconnectRequest();
        if (disconnect is { Length: > 0 })
        {
            await SendFrameAsync(media, disconnect, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<object?> ReadValueAsync(
        GXDLMSClient client,
        GXNet media,
        GXDLMSObject target,
        int attributeIndex,
        CancellationToken cancellationToken)
    {
        var frames = client.Read(target, attributeIndex);
        var reply = new GXReplyData();
        await SendRequestFramesAsync(client, media, frames, cancellationToken, reply).ConfigureAwait(false);
        return reply.Value;
    }

    private async Task SendRequestFramesAsync(
        GXDLMSClient client,
        GXNet media,
        byte[][]? frames,
        CancellationToken cancellationToken,
        GXReplyData? reply = null)
    {
        if (frames is null)
        {
            return;
        }

        foreach (var frame in frames)
        {
            if (frame is null || frame.Length == 0)
            {
                continue;
            }

            await SendRequestFrameAsync(client, media, frame, reply ?? new GXReplyData(), cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task SendRequestFrameAsync(
        GXDLMSClient client,
        GXNet media,
        byte[]? frame,
        GXReplyData reply,
        CancellationToken cancellationToken)
    {
        if (frame is null || frame.Length == 0)
        {
            return;
        }

        await SendFrameAsync(media, frame, cancellationToken).ConfigureAwait(false);
        await ReceiveReplyAsync(client, media, reply, cancellationToken).ConfigureAwait(false);
    }

    private Task SendFrameAsync(GXNet media, byte[] frame, CancellationToken cancellationToken) =>
        Task.Run(() => media.Send(frame, null), cancellationToken);

    private async Task ReceiveReplyAsync(GXDLMSClient client, GXNet media, GXReplyData reply, CancellationToken cancellationToken)
    {
        reply.Clear();
        var notify = new GXReplyData();
        var parameters = new ReceiveParameters<byte[]>
        {
            AllData = true,
            Eop = (byte)0x7E,
            WaitTime = _options.WaitTime,
            Count = _options.ReceiveCount
        };

        do
        {
            parameters.Reply = null;
            var received = await Task.Run(() => media.Receive(parameters), cancellationToken).ConfigureAwait(false);
            if (!received || parameters.Reply is null)
            {
                throw new IOException("DLMS server did not respond to request.");
            }

            var buffer = new GXByteBuffer(parameters.Reply);
            client.GetData(buffer, reply, notify);
        }
        while (reply.MoreData != RequestTypes.None);
    }

    private sealed class AsyncDisposableMedia : IAsyncDisposable
    {
        public GXNet Media { get; }

        public AsyncDisposableMedia(GXNet media)
        {
            Media = media;
        }

        public ValueTask DisposeAsync()
        {
            Media.Close();
            Media.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
