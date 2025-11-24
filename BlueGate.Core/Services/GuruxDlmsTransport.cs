using System.IO;
using BlueGate.Core.Configuration;
using BlueGate.Core.Models;
using Gurux.Common;
using Gurux.DLMS;
using Gurux.DLMS.Enums;
using Gurux.DLMS.Objects;
using Gurux.Net;
using Microsoft.Extensions.Logging;
using Task = System.Threading.Tasks.Task;

namespace BlueGate.Core.Services;

public class GuruxDlmsTransport : IDlmsTransport
{
    private readonly ILogger<GuruxDlmsTransport> _logger;

    public GuruxDlmsTransport(ILogger<GuruxDlmsTransport> logger)
    {
        _logger = logger;
    }

    public async Task<IEnumerable<CosemObject>> ReadAllAsync(
        DlmsClientOptions options,
        CancellationToken cancellationToken = default)
    {
        var results = new List<CosemObject>();

        await using var media = new AsyncDisposableMedia(CreateMedia(options));
        var client = CreateClient(options);

        try
        {
            await OpenAsync(media.Media, cancellationToken).ConfigureAwait(false);
            await EstablishAssociationAsync(client, media.Media, options, cancellationToken).ConfigureAwait(false);

            var register = new GXDLMSRegister("1.0.1.8.0.255");
            var value = await ReadValueAsync(client, media.Media, register, 2, options, cancellationToken).ConfigureAwait(false);

            results.Add(new CosemObject
            {
                ObisCode = register.LogicalName,
                Value = value,
                Timestamp = DateTime.UtcNow
            });

            await ReleaseAssociationAsync(client, media.Media, options, cancellationToken).ConfigureAwait(false);
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

    public async Task WriteAsync(
        DlmsClientOptions options,
        string obisCode,
        object value,
        CancellationToken cancellationToken = default)
    {
        await using var media = new AsyncDisposableMedia(CreateMedia(options));
        var client = CreateClient(options);

        try
        {
            await OpenAsync(media.Media, cancellationToken).ConfigureAwait(false);
            await EstablishAssociationAsync(client, media.Media, options, cancellationToken).ConfigureAwait(false);

            var register = new GXDLMSRegister(obisCode);
#pragma warning disable CS0618
            var writeFrames = client.Write(register, value, DataType.None, ObjectType.Register, 2);
#pragma warning restore CS0618
            await SendRequestFramesAsync(client, media.Media, writeFrames, options, cancellationToken).ConfigureAwait(false);

            await ReleaseAssociationAsync(client, media.Media, options, cancellationToken).ConfigureAwait(false);
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

    private GXDLMSClient CreateClient(DlmsClientOptions options) => new(
        useLogicalNameReferencing: true,
        clientAddress: options.ClientAddress,
        serverAddress: options.ServerAddress,
        authentication: options.Authentication,
        password: options.Password,
        interfaceType: options.InterfaceType
    );

    private GXNet CreateMedia(DlmsClientOptions options) => new(NetworkType.Tcp, options.Host, options.Port)
    {
        WaitTime = options.WaitTime
    };

    private static Task OpenAsync(GXNet media, CancellationToken cancellationToken) =>
        Task.Run(media.Open, cancellationToken);

    private static Task CloseAsync(GXNet media, CancellationToken cancellationToken) =>
        Task.Run(media.Close, cancellationToken);

    private async Task EstablishAssociationAsync(
        GXDLMSClient client,
        GXNet media,
        DlmsClientOptions options,
        CancellationToken cancellationToken)
    {
        var reply = new GXReplyData();

        await SendRequestFrameAsync(client, media, client.SNRMRequest(false), reply, options, cancellationToken).ConfigureAwait(false);
        if (reply.Data.Size != 0)
        {
            client.ParseUAResponse(reply.Data);
        }

        reply.Clear();
        await SendRequestFramesAsync(client, media, client.AARQRequest(), options, cancellationToken, reply).ConfigureAwait(false);
        client.ParseAAREResponse(reply.Data);

        reply.Clear();
        var associationRequest = client.GetApplicationAssociationRequest();
        if (associationRequest is not null && associationRequest.Length > 0)
        {
            await SendRequestFramesAsync(client, media, associationRequest, options, cancellationToken, reply).ConfigureAwait(false);
            client.ParseApplicationAssociationResponse(reply.Data);
        }
    }

    private async Task ReleaseAssociationAsync(
        GXDLMSClient client,
        GXNet media,
        DlmsClientOptions options,
        CancellationToken cancellationToken)
    {
        var reply = new GXReplyData();
        await SendRequestFramesAsync(client, media, client.ReleaseRequest(), options, cancellationToken, reply).ConfigureAwait(false);
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
        DlmsClientOptions options,
        CancellationToken cancellationToken)
    {
        var frames = client.Read(target, attributeIndex);
        var reply = new GXReplyData();
        await SendRequestFramesAsync(client, media, frames, options, cancellationToken, reply).ConfigureAwait(false);
        return reply.Value;
    }

    private async Task SendRequestFramesAsync(
        GXDLMSClient client,
        GXNet media,
        byte[][]? frames,
        DlmsClientOptions options,
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

            await SendRequestFrameAsync(client, media, frame, reply ?? new GXReplyData(), options, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task SendRequestFrameAsync(
        GXDLMSClient client,
        GXNet media,
        byte[]? frame,
        GXReplyData reply,
        DlmsClientOptions options,
        CancellationToken cancellationToken)
    {
        if (frame is null || frame.Length == 0)
        {
            return;
        }

        await SendFrameAsync(media, frame, cancellationToken).ConfigureAwait(false);
        await ReceiveReplyAsync(client, media, reply, options, cancellationToken).ConfigureAwait(false);
    }

    private Task SendFrameAsync(GXNet media, byte[] frame, CancellationToken cancellationToken) =>
        Task.Run(() => media.Send(frame, null), cancellationToken);

    private async Task ReceiveReplyAsync(
        GXDLMSClient client,
        GXNet media,
        GXReplyData reply,
        DlmsClientOptions options,
        CancellationToken cancellationToken)
    {
        reply.Clear();
        var notify = new GXReplyData();
        var parameters = new ReceiveParameters<byte[]>
        {
            AllData = true,
            Eop = (byte)0x7E,
            WaitTime = options.WaitTime,
            Count = options.ReceiveCount
        };

        do
        {
            parameters.Reply = Array.Empty<byte>();
            var received = await Task.Run(() => media.Receive(parameters), cancellationToken).ConfigureAwait(false);
            if (!received || parameters.Reply.Length == 0)
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
