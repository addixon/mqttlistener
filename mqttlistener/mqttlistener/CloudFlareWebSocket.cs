using Microsoft.Extensions.Options;

namespace mqttlistener;

using MQTTnet.Client;
using System;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

public class CloudFlareWebSocket : IMqttClientAdapter
{
    private readonly ClientWebSocket _clientWebSocket;
    private readonly MqttClientOptions _options;

    public CloudFlareWebSocket(MqttClientOptions options, IOptions<CloudFlareConfiguration> cloudflareConfiguration)
    {
        _options = options;
        _clientWebSocket = new ClientWebSocket();

        // Add custom headers
        _clientWebSocket.Options.SetRequestHeader("CF-Access-Client-Id", cloudflareConfiguration.Value.CloudFlareId);
        _clientWebSocket.Options.SetRequestHeader("CF-Access-Client-Secret", cloudflareConfiguration.Value.CloudFlareSecret);
    }

    public async Task ConnectAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        var uri = new Uri(_options.ChannelOptions.TlsOptions.TargetHost);
        await _clientWebSocket.ConnectAsync(uri, cancellationToken).ConfigureAwait(false);
    }

    public async Task DisconnectAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        await _clientWebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, cancellationToken).ConfigureAwait(false);
    }

    public void Dispose()
    {
        _clientWebSocket.Dispose();
    }

    public async Task<int> ReceiveAsync(byte[] buffer, int offset, int length, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var segment = new ArraySegment<byte>(buffer, offset, length);
        var result = await _clientWebSocket.ReceiveAsync(segment, cancellationToken).ConfigureAwait(false);
        return result.Count;
    }

    public async Task SendAsync(byte[] buffer, int offset, int length, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var segment = new ArraySegment<byte>(buffer, offset, length);
        await _clientWebSocket.SendAsync(segment, WebSocketMessageType.Binary, true, cancellationToken).ConfigureAwait(false);
    }
}
