namespace mqttlistener;

public class MqttConfiguration
{
    public string BrokerUrl { get; set; } = "emqx.adamdixon.dev:8083/mqtt";
    public string BrokerUsername { get; set; } = "devopsmonitor";
    public string BrokerPassword { get; set; }
}