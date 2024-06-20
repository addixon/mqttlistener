using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using mqttlistener;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Extensions.WebSocket4Net;

public class Program
{
    public static async Task Main(string[] args)
    {
        var host = new HostBuilder()
    .ConfigureFunctionsWebApplication().ConfigureHostConfiguration(builder =>
    {
        builder.AddUserSecrets<Program>();
    })
    .ConfigureServices((context, services) =>
    {
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();

        services.Configure<MqttConfiguration>(context.Configuration.GetSection("mqtt"));
        services.Configure<CloudFlareConfiguration>(context.Configuration.GetSection("cloudflare"));

        services.AddSingleton<MqttClientOptions>(sp =>
        {
            MqttConfiguration mqttConfig = sp.GetRequiredService<IOptions<MqttConfiguration>>().Value;
            CloudFlareConfiguration cfConfig = sp.GetRequiredService<IOptions<CloudFlareConfiguration>>().Value;

            string broker = mqttConfig.BrokerUrl;
            string clientId = $"devops-listener";
            string username = mqttConfig.BrokerUsername;
            string password = mqttConfig.BrokerPassword;

            return new MqttClientOptionsBuilder()
                .WithWebSocketServer(webSocketOptions =>
                {
                    webSocketOptions.WithUri(broker);
                    webSocketOptions.WithRequestHeaders(new Dictionary<string, string>()
                    {
                        {"CF-Access-Client-Id", cfConfig.CloudFlareId},
                        {"CF-Access-Client-Secret", cfConfig.CloudFlareSecret}
                    });
                })
                .WithCredentials(username, password) // Set username and password
                .WithClientId(clientId)
                .WithCleanSession()
                .Build();
        });

        services.AddScoped<IMqttClient>(sp =>
        {
            // Create a MQTT client factory
            var factory = new MqttFactory().UseWebSocket4Net();

            // Create a MQTT client instance 
            var mqttClient = factory.CreateMqttClient();

            Console.WriteLine("Connecting to MQTT broker...");

            var connectResult = mqttClient.ConnectAsync(sp.GetRequiredService<MqttClientOptions>(), CancellationToken.None).Result;

            if (connectResult.ResultCode != MqttClientConnectResultCode.Success)
            {
                throw new Exception($"Failed to connect to MQTT broker: {connectResult.ReasonString}");
            }

            Console.WriteLine("The MQTT client is connected.");
            return mqttClient;
        });
    })
    .Build();

        await host.RunAsync();

    }


}