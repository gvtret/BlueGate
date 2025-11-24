using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using BlueGate.Core.Configuration;
using BlueGate.Core.Models;
using Gurux.Common;
using Gurux.DLMS;
using Gurux.DLMS.Enums;
using Gurux.DLMS.Objects;
using Gurux.DLMS.Objects.Enums;
using Gurux.DLMS.Secure;
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
        IEnumerable<MappingProfile> profiles,
        CancellationToken cancellationToken = default)
    {
        var results = new List<CosemObject>();
        var targetProfiles = profiles.ToList();

        if (targetProfiles.Count == 0)
        {
            _logger.LogWarning("No DLMS mapping profiles configured; skipping read.");
            return results;
        }

        EnsureSecurityConfiguration(options);
        await using var media = new AsyncDisposableMedia(CreateMedia(options));
        var client = CreateClient(options);

        try
        {
            await OpenAsync(media.Media, cancellationToken).ConfigureAwait(false);
            await EstablishAssociationAsync(client, media.Media, options, cancellationToken).ConfigureAwait(false);

            foreach (var profile in targetProfiles)
            {
                var target = CreateDlmsObject(profile);
                var value = await ReadValueAsync(client, media.Media, target, profile.AttributeIndex, options, cancellationToken)
                    .ConfigureAwait(false);

                results.Add(new CosemObject
                {
                    ObisCode = target.LogicalName,
                    Name = profile.OpcNodeId,
                    Value = value,
                    Timestamp = DateTime.UtcNow
                });
            }

            await ReleaseAssociationAsync(client, media.Media, options, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DLMS read error");
        }
        finally
        {
            PersistInvocationCounter(client, options);
            await CloseAsync(media.Media, cancellationToken).ConfigureAwait(false);
        }

        return results;
    }

    public async Task WriteAsync(
        DlmsClientOptions options,
        string obisCode,
        IEnumerable<MappingProfile> profiles,
        object value,
        CancellationToken cancellationToken = default)
    {
        var profile = profiles.FirstOrDefault(p => p.ObisCode == obisCode);
        if (profile is null)
        {
            throw new ArgumentException($"OBIS code {obisCode} is not configured for DLMS access.", nameof(obisCode));
        }

        EnsureSecurityConfiguration(options);
        await using var media = new AsyncDisposableMedia(CreateMedia(options));
        var client = CreateClient(options);

        try
        {
            await OpenAsync(media.Media, cancellationToken).ConfigureAwait(false);
            await EstablishAssociationAsync(client, media.Media, options, cancellationToken).ConfigureAwait(false);

            var target = CreateDlmsObject(profile);
#pragma warning disable CS0618
            var writeFrames = client.Write(target, value, DataType.None, profile.ObjectType, profile.AttributeIndex);
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
            PersistInvocationCounter(client, options);
            await CloseAsync(media.Media, cancellationToken).ConfigureAwait(false);
        }
    }

    private GXDLMSClient CreateClient(DlmsClientOptions options)
    {
        var client = new GXDLMSClient(
            useLogicalNameReferencing: true,
            clientAddress: options.ClientAddress,
            serverAddress: options.ServerAddress,
            authentication: options.Authentication,
            password: options.Password,
            interfaceType: options.InterfaceType);

        ConfigureCiphering(client, options);
        return client;
    }

    private static GXDLMSObject CreateDlmsObject(MappingProfile profile)
    {
        var target = GXDLMSClient.CreateObject(profile.ObjectType)
            ?? throw new InvalidOperationException($"Unsupported DLMS object type: {profile.ObjectType}");

        target.LogicalName = profile.ObisCode;
        return target;
    }

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

    private void EnsureSecurityConfiguration(DlmsClientOptions options)
    {
        if (options.SecuritySuite == SecuritySuite.Suite0)
        {
            _logger.LogInformation("Using DLMS security suite {SecuritySuite} (no ciphering configured).", options.SecuritySuite);
            return;
        }

        var missing = new List<string>();

        if (string.IsNullOrWhiteSpace(options.BlockCipherKey))
        {
            missing.Add(nameof(options.BlockCipherKey));
        }

        if (string.IsNullOrWhiteSpace(options.AuthenticationKey))
        {
            missing.Add(nameof(options.AuthenticationKey));
        }

        if (string.IsNullOrWhiteSpace(options.SystemTitle))
        {
            missing.Add(nameof(options.SystemTitle));
        }

        if (string.IsNullOrWhiteSpace(options.InvocationCounterPath))
        {
            missing.Add(nameof(options.InvocationCounterPath));
        }

        if (missing.Count > 0)
        {
            var error = $"Security suite {options.SecuritySuite} requires: {string.Join(", ", missing)}.";
            _logger.LogError(error);
            throw new InvalidOperationException(error);
        }
    }

    private void ConfigureCiphering(GXDLMSClient client, DlmsClientOptions options)
    {
        var ciphering = GetCiphering(client) ?? new GXCiphering();

        ciphering.SecuritySuite = options.SecuritySuite;
        ciphering.Security = options.SecuritySuite == SecuritySuite.Suite0
            ? Security.None
            : Security.AuthenticationEncryption;
        ciphering.BlockCipherKey = ParseOptionalHex(options.BlockCipherKey);
        ciphering.AuthenticationKey = ParseOptionalHex(options.AuthenticationKey);
        ciphering.SystemTitle = ParseOptionalHex(options.SystemTitle);

        if (!string.IsNullOrWhiteSpace(options.InvocationCounterPath) && options.SecuritySuite != SecuritySuite.Suite0)
        {
            ciphering.InvocationCounter = LoadInvocationCounter(options.InvocationCounterPath);
        }

        if (ciphering.SystemTitle is { Length: > 0 })
        {
            SetSourceSystemTitle(client, ciphering.SystemTitle);
        }

        SetCiphering(client, ciphering);
        _logger.LogInformation("Configured DLMS ciphering: suite {SecuritySuite}, security {Security}.", ciphering.SecuritySuite, ciphering.Security);
    }

    private static byte[]? ParseOptionalHex(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return Convert.FromHexString(value);
    }

    private uint LoadInvocationCounter(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                var content = File.ReadAllText(path).Trim();
                if (uint.TryParse(content, NumberStyles.Integer, CultureInfo.InvariantCulture, out var counter))
                {
                    return counter;
                }

                _logger.LogWarning("Invocation counter file {Path} contained invalid data; starting from zero.", path);
            }
            else
            {
                _logger.LogInformation("Invocation counter file {Path} not found; starting from zero.", path);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read invocation counter from {Path}; starting from zero.", path);
        }

        return 0;
    }

    private void PersistInvocationCounter(GXDLMSClient client, DlmsClientOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.InvocationCounterPath))
        {
            return;
        }

        if (GetCiphering(client) is not GXCiphering ciphering)
        {
            return;
        }

        try
        {
            var directory = Path.GetDirectoryName(options.InvocationCounterPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(options.InvocationCounterPath, ciphering.InvocationCounter.ToString(CultureInfo.InvariantCulture));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to persist DLMS invocation counter to {Path}.", options.InvocationCounterPath);
        }
    }

    private GXCiphering? GetCiphering(GXDLMSClient client)
    {
        var cipherProperty = client.Settings.GetType().GetProperty("Cipher", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        return cipherProperty?.GetValue(client.Settings) as GXCiphering;
    }

    private void SetCiphering(GXDLMSClient client, GXCiphering ciphering)
    {
        var cipherProperty = client.Settings.GetType().GetProperty("Cipher", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (cipherProperty is null)
        {
            _logger.LogWarning("GXDLMS client does not expose a Cipher property; security settings will not be applied.");
            return;
        }

        cipherProperty.SetValue(client.Settings, ciphering);
    }

    private void SetSourceSystemTitle(GXDLMSClient client, byte[] systemTitle)
    {
        var systemTitleProperty = client.Settings.GetType().GetProperty("SourceSystemTitle", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (systemTitleProperty is null)
        {
            _logger.LogWarning("GXDLMS client does not expose a writable SourceSystemTitle; ciphering system title was not applied.");
            return;
        }

        systemTitleProperty.SetValue(client.Settings, systemTitle);
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
