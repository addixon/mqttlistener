using Microsoft.AspNet.WebHooks.Payloads;
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
        private readonly IList<string> _interestingAssignees = new List<string>
        {
            "adam.dixon <adam.dixon@cnhind.com>",
            "iryna.onishchuk <iryna.onishchuk@external.cnhind.com>",

        };

        [Function("Forward")]
        public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequest req)
        {
            logger.LogInformation("Handling MQTT Forwarding.");

            if (!req.Query.TryGetValue("topic", out var topic))
            {
                logger.LogWarning("No topic specified.");
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
                logger.LogWarning("No body in request.");
                return new BadRequestResult();
            }

            try
            {

                using var jsonDoc = JsonDocument.Parse(body);

                if (!jsonDoc.RootElement.TryGetProperty("eventType", out JsonElement eventType))
                {
                    return new BadRequestResult();
                }

                if (eventType.GetString() != "workitem.updated")
                {
                    logger.LogInformation("Ignoring event type: {EventType}", eventType.GetString());
                    return new OkResult();
                }

                WorkItemUpdatedPayload? payload;

                try
                {
                    payload = JsonSerializer.Deserialize<WorkItemUpdatedPayload>(body);
                }
                catch (Exception e)
                {
                    logger.LogError(e, "Failed to deserialize payload.");
                    return new BadRequestResult();
                }

                if (payload is null)
                {
                    logger.LogWarning("Could not deserialize payload.");
                    return new BadRequestResult();
                }

                //if (payload.Resource?.Revision?.Fields?.SystemWorkItemType != "Bug")
                //{
                //    logger.LogInformation("Ignoring work item type: {WorkItemType}", payload.Resource?.Revision?.Fields?.SystemWorkItemType);
                //    return new OkResult();
                //}

                logger.LogInformation("Received work item update for WorkItemId: {WorkItemId}. Payload: {@Payload}. Body: {Body}", payload.Resource?.WorkItemId, payload, body);

                string? assignee = payload.Resource?.Fields?.SystemAssignedTo?.NewValue;

                if (string.IsNullOrWhiteSpace(assignee))
                {
                    logger.LogInformation("Ignoring work item with no modified assignee.");
                    return new OkResult();
                }

                if (!_interestingAssignees.Contains(assignee))
                {
                    logger.LogInformation("Ignoring work item assigned to: {Assignee}", assignee);
                    return new OkResult();
                }

                var mqttMessage = new
                {
                    WorkItemId = payload.Resource!.WorkItemId,
                    Type = payload.Resource.Revision?.Fields?.SystemWorkItemType,
                    Url = payload.Resource.Revision?.Url,
                    Severity = payload.Resource.Revision?.Fields?.MicrosoftCommonSeverity,
                };

                logger.LogInformation("Forwarding message to MQTT broker: {Topic} for WorkItemId: {WorkItemId}", topic, mqttMessage.WorkItemId);

                var message = new MqttApplicationMessageBuilder()
                    .WithTopic(topic)
                    .WithPayload(JsonSerializer.Serialize(mqttMessage))
                    .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
                    .WithRetainFlag()
                    .Build();

                await mqttClient.PublishAsync(message);
            }
            catch (JsonException e)
            {
                logger.LogError(e, "Failed to parse JSON.");
                return new BadRequestResult();
            }

            return new OkResult();
        }
    }
}
