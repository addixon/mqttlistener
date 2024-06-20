namespace mqttlistener;

public interface IMqttClientAdapter
{
    public Task ConnectAsync(TimeSpan timeout, CancellationToken cancellationToken);

    public Task DisconnectAsync(TimeSpan timeout, CancellationToken cancellationToken);

    public void Dispose();

    public Task<int> ReceiveAsync(byte[] buffer, int offset, int length, TimeSpan timeout,
        CancellationToken cancellationToken);

    public Task SendAsync(byte[] buffer, int offset, int length, TimeSpan timeout,
        CancellationToken cancellationToken);

}