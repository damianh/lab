using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Pulumi;
using Pulumi.Aws;
using Pulumi.Aws.Iam;
using Pulumi.Aws.Inputs;
using Pulumi.Aws.Lambda;
using Pulumi.Aws.Lambda.Inputs;
using Pulumi.Aws.Sns;

class Program
{
    static Task<int> Main()
    {
        var config = new ConfigurationBuilder()
            .AddUserSecrets(typeof(Program).Assembly)
            .Build();

        var accountId = config.GetValue<string>("AccountId");
        var role = config.GetValue<string>("Role");

        return Deployment.RunAsync(() =>
        {
            var roleArn = Output.Format($"arn:aws:iam::{accountId}:role/{role}");

            var providerArgs = new ProviderArgs
            {
                Region = "eu-west-1",
                AssumeRole = new ProviderAssumeRoleArgs
                {
                    RoleArn = roleArn,
                    SessionName = "session"
                }
            };
            
            var provider = new Provider("sandboxEUWest1Provider", providerArgs);

            var options = new CustomResourceOptions
            {
                Provider = provider
            };

            // Worker SNS Notification
            var notificationTopicArgs = new TopicArgs
            {
                Name = "WorkerNotification",
            };
            var workerNotificationTopic = new Topic("workerNotification", notificationTopicArgs, options);
            
            // Lambda
            var functionType = typeof(Worker.Function);
            var handler = $"{functionType.Assembly.GetName().Name}::{functionType.FullName}::FunctionHandler";

            var function = new Function("WorkerFunction", new FunctionArgs
            {
                Name = "WorkerFunction",
                Runtime = "dotnetcore3.1",
                Environment = new FunctionEnvironmentArgs
                {
                    Variables = new InputMap<string>
                    {
                        {$"{Worker.Function.NotificationTopicArnKey}", workerNotificationTopic.Arn}
                    }
                },
                Code = new FileArchive("../Worker/bin/Release/netcoreapp3.1/publish"),
                ReservedConcurrentExecutions = 1,
                Handler = handler,
                Role = CreateLambdaRole(options).Arn,
                MemorySize = 512,
                Timeout = 60
            },options);

            var permission = new Permission("WorkerSNSPermission", new PermissionArgs
            {
                Action = "lambda:InvokeFunction",
                Function = function.Arn,
                Principal = "sns.amazonaws.com",
                SourceArn = workerNotificationTopic.Arn,
            }, options);

            // Subscription
            var workerSubscription = new TopicSubscription("workerNotificationSubscription", new TopicSubscriptionArgs
            {
                Topic = workerNotificationTopic.Arn,
                Protocol = "lambda",
                Endpoint = function.Arn
            }, options);
        });
    }

    private static Role CreateLambdaRole(CustomResourceOptions options)
    {
        var lambdaRole = new Role("workerLambdaRole", new RoleArgs
        {
            Name = "WorkerLambdaRole",
            AssumeRolePolicy =
                @"{
                ""Version"": ""2012-10-17"",
                ""Statement"": [
                    {
                        ""Action"": ""sts:AssumeRole"",
                        ""Principal"": {
                            ""Service"": ""lambda.amazonaws.com""
                        },
                        ""Effect"": ""Allow"",
                        ""Sid"": """"
                    }
                ]
            }"
        }, options);

        var logPolicy = new RolePolicy("workerLambdaLogPolicy", new RolePolicyArgs
        {
            Name = "Log",
            Role = lambdaRole.Id,
            Policy =
                @"{
                ""Version"": ""2012-10-17"",
                ""Statement"": [{
                    ""Effect"": ""Allow"",
                    ""Action"": [
                        ""logs:CreateLogGroup"",
                        ""logs:CreateLogStream"",
                        ""logs:PutLogEvents""
                    ],
                    ""Resource"": ""arn:aws:logs:*:*:*""
                }]
            }"
        }, options);

        var snsPublishPolicy = new RolePolicy("workerSNSPublishPolicy", new RolePolicyArgs
        {
            Name = "SNSPublish",
            Role = lambdaRole.Id,
            Policy =
                @"{
                ""Version"": ""2012-10-17"",
                ""Statement"": [{
                    ""Effect"": ""Allow"",
                    ""Action"": [
                        ""sns:Publish""
                    ],
                    ""Resource"": ""arn:aws:sns:*:*:*""
                }]
            }"
        }, options);

        return lambdaRole;
    }
}
