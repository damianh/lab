using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Amazon.Lambda.Core;
using Amazon.Lambda.SNSEvents;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Microsoft.Extensions.Configuration;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace Worker
{
    public class Function
    {
        public const string NotificationTopicArnKey = "NotificationTopicArn";
        private readonly string _notificationTopicArn;

        public Function()
        {
            var config = new ConfigurationBuilder()
                .AddEnvironmentVariables()
                .Build();
            _notificationTopicArn = config.GetValue<string>("NotificationTopicArn");
        }

        /// <summary>
        /// This method is called for every Lambda invocation. This method takes in an SNS event object and can be used 
        /// to respond to SNS messages.
        /// </summary>
        /// <param name="event"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        public async Task FunctionHandler(SNSEvent @event, ILambdaContext context)
        {
            context.Logger.LogLine($"Record count {@event.Records.Count}");

            int i = 0;
            var notifications = new List<NotificationDto>();
            foreach(var record in @event.Records)
            {
                var notificationDto = JsonSerializer.Deserialize<NotificationDto>(record.Sns.Message);
                notifications.Add(notificationDto);
                context.Logger.LogLine($"Record {i}. Counter {notificationDto.Counter}. RepeatUntil {notificationDto.RepeatUntil}");
            }
            // We don't care about processing each record (there should only be 1 anyway).

            var notification = notifications
                .OrderByDescending(n => n.Counter)
                .First();

            context.Logger.LogLine($"Record {i}. Counter {notification.Counter}. RepeatUntil {notification.RepeatUntil}");

            //await Task.Delay(TimeSpan.FromSeconds(2)); // Pretend work

            var amazonSimpleNotificationServiceClient = new AmazonSimpleNotificationServiceClient();

            if (notification.Counter >= notification.RepeatUntil)
            {
                context.Logger.LogLine("Reached the end.");
                return;
            }

            var newNotification = new NotificationDto
            {
                Counter = notification.Counter + 1,
                RepeatUntil = notification.RepeatUntil
            };
            var message = JsonSerializer.Serialize(newNotification);
            var publishRequest = new PublishRequest(_notificationTopicArn, message, "notification")
            {
                MessageGroupId = "25AC37D6-725B-4DD5-85AC-215A4CBB8A79",
                MessageStructure = "json",
                Subject = "resume"
            };

            context.Logger.LogLine($"Publishing notification to continue (counter = {newNotification.Counter})");
            await amazonSimpleNotificationServiceClient.PublishAsync(_notificationTopicArn, message);
        }
    }

    public class NotificationDto
    {
        public int Counter { get; set; }

        public int RepeatUntil { get; set; }
    }
}
