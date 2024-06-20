using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Protocol;
using System.Text.Json;

namespace mqttlistener
{
    public class Forward(IMqttClient mqttClient, ILogger<Forward> logger)
    {
        [Function("Forward")]
        public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequest req)
        {
            logger.LogInformation("C# HTTP trigger function processed a request.");

            if (!req.Query.TryGetValue("topic", out var topic))
            {
                return new BadRequestResult();
            }

            if (!req.Body.CanSeek)
            {
                req.EnableBuffering();
            }

            req.Body.Position = 0;
            string body = await new StreamReader(req.Body).ReadToEndAsync();
            
            if (string.IsNullOrWhiteSpace(body))
            {
                return new BadRequestResult();
            }

            try
            {
                using var jsonDoc = JsonDocument.Parse(body);

                var message = new MqttApplicationMessageBuilder()
                    .WithTopic(topic)
                    .WithPayload(jsonDoc.RootElement.GetRawText())
                    .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
                    .WithRetainFlag()
                    .Build();

                await mqttClient.PublishAsync(message);
            }
            catch (JsonException)
            {
                return new BadRequestResult();
            }

            return new OkResult();
        }
    }
}
