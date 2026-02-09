using System.Text;
using System.Text.Json;
using ZeroInstall.Agent.Models;
using ZeroInstall.Core.Services;

namespace ZeroInstall.Agent.Services;

/// <summary>
/// Protocol helpers for agent handshake and completion messages.
/// Uses sentinel RelativePath values over the existing ITransport.SendAsync/ReceiveAsync.
/// </summary>
internal static class AgentProtocol
{
    internal const string HandshakePath = "__HANDSHAKE__";
    internal const string HandshakeResponsePath = "__HANDSHAKE_RESPONSE__";
    internal const string TransferCompletePath = "__TRANSFER_COMPLETE__";

    public static async Task SendHandshakeAsync(ITransport transport, AgentHandshake handshake, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(handshake);
        var data = Encoding.UTF8.GetBytes(json);
        using var stream = new MemoryStream(data);
        var metadata = new TransferMetadata
        {
            RelativePath = HandshakePath,
            SizeBytes = data.Length
        };
        await transport.SendAsync(stream, metadata, ct: ct);
    }

    public static async Task<AgentHandshake> ReceiveHandshakeAsync(ITransport transport, CancellationToken ct)
    {
        var metadata = new TransferMetadata { RelativePath = HandshakePath };
        using var stream = await transport.ReceiveAsync(metadata, ct);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        var json = await reader.ReadToEndAsync(ct);
        return JsonSerializer.Deserialize<AgentHandshake>(json)
            ?? throw new InvalidDataException("Failed to deserialize handshake");
    }

    public static async Task SendHandshakeResponseAsync(ITransport transport, AgentHandshakeResponse response, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(response);
        var data = Encoding.UTF8.GetBytes(json);
        using var stream = new MemoryStream(data);
        var metadata = new TransferMetadata
        {
            RelativePath = HandshakeResponsePath,
            SizeBytes = data.Length
        };
        await transport.SendAsync(stream, metadata, ct: ct);
    }

    public static async Task<AgentHandshakeResponse> ReceiveHandshakeResponseAsync(ITransport transport, CancellationToken ct)
    {
        var metadata = new TransferMetadata { RelativePath = HandshakeResponsePath };
        using var stream = await transport.ReceiveAsync(metadata, ct);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        var json = await reader.ReadToEndAsync(ct);
        return JsonSerializer.Deserialize<AgentHandshakeResponse>(json)
            ?? throw new InvalidDataException("Failed to deserialize handshake response");
    }

    public static async Task SendCompletionAsync(ITransport transport, CancellationToken ct)
    {
        var data = Encoding.UTF8.GetBytes("DONE");
        using var stream = new MemoryStream(data);
        var metadata = new TransferMetadata
        {
            RelativePath = TransferCompletePath,
            SizeBytes = data.Length
        };
        await transport.SendAsync(stream, metadata, ct: ct);
    }

    /// <summary>
    /// Checks whether a received TransferMetadata represents the completion sentinel.
    /// </summary>
    public static bool IsCompletionFrame(TransferMetadata metadata)
    {
        return metadata.RelativePath == TransferCompletePath;
    }
}
