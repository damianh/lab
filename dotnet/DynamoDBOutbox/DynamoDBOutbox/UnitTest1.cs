using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DocumentModel;
using Amazon.DynamoDBv2.Model;
using Amazon.Runtime;
using Ductus.FluentDocker.Builders;
using Ductus.FluentDocker.Services;
using Xunit;
using Xunit.Abstractions;

namespace DynamoDBOutbox
{
    public class UnitTest1 : IAsyncLifetime
    {
        private readonly ITestOutputHelper _testOutputHelper;
        private readonly IContainerService _service;
        private readonly AmazonDynamoDBClient _client;
        private readonly AmazonDynamoDBStreamsClient _streamsClient;
        private string _tableLatestStreamArn;
        private const string OutboxTableName = "Outbox";
        private const string OrgsTableName = "Orgs";

        public UnitTest1(ITestOutputHelper testOutputHelper)
        {
            _testOutputHelper = testOutputHelper;
            var containerService = new Builder()
                .UseContainer()
                .WithName("dynamodb-outbox")
                .UseImage("amazon/dynamodb-local:1.13.0")
                .KeepRunning()
                .ReuseIfExists()
                .Command("", "-jar", "DynamoDBLocal.jar", "-inMemory", "-sharedDb")
                .ExposePort(8000, 8000)
                .WaitForPort("8000/tcp", 5000, "127.0.0.1")
                .Build();

            _service = containerService.Start();

            var awsCredentials = new BasicAWSCredentials("ignored", "ignored");
            var dynamoDBConfig = new AmazonDynamoDBConfig
            {
                ServiceURL = "http://localhost:8000",
            };
            _client = new AmazonDynamoDBClient(awsCredentials, dynamoDBConfig);

            var streamsConfig = new AmazonDynamoDBStreamsConfig
            {
                ServiceURL = dynamoDBConfig.ServiceURL
            };
            _streamsClient = new AmazonDynamoDBStreamsClient(awsCredentials, streamsConfig);
        }

        [Fact]
        public async Task Test1()
        {
            // Transaction put with an outbox message.
            var transactWriteItems = new List<TransactWriteItem>();

            for (int i = 0; i < 5; i++)
            {
                var orgDoc = new Document
                {
                    ["Id"] = 123,
                    ["Name"] = $"org-123-{i}",
                    ["Foo"] = Document.FromJson("{\"plot\" : \"Nothing happens at all.\",\"rating\" : 0}")
                };

                transactWriteItems.Add(new TransactWriteItem
                {
                    Put = new Put
                    {
                        TableName = OrgsTableName,
                        Item = orgDoc.ToAttributeMap()
                    }
                });

                var outboxDoc = new Document
                {
                    ["Id"] = $"123-{i}",
                    ["Created"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    ["Body"] = Document.FromJson("{\"plot\" : \"Nothing happens at all.\",\"rating\" : 0}")
                };

                transactWriteItems.Add(new TransactWriteItem
                {
                    Put = new Put
                    {
                        TableName = OutboxTableName,
                        Item = outboxDoc.ToAttributeMap()
                    }
                });
            }

            var transactWriteItemsRequest = new TransactWriteItemsRequest
            {
                TransactItems = transactWriteItems,
            };
            await _client.TransactWriteItemsAsync(transactWriteItemsRequest);


            // Read the changes to outbox change stream
            string lastEvaluatedShardId = null;
            do
            {
                var describeStreamRequest = new DescribeStreamRequest
                {
                    StreamArn = _tableLatestStreamArn,
                    ExclusiveStartShardId = lastEvaluatedShardId
                };

                var describeStreamResponse = await _streamsClient.DescribeStreamAsync(describeStreamRequest);
                foreach (var shard in describeStreamResponse.StreamDescription.Shards)
                {
                    var getShardIteratorRequest = new GetShardIteratorRequest
                    {
                        StreamArn = _tableLatestStreamArn,
                        ShardId = shard.ShardId,
                        ShardIteratorType = ShardIteratorType.TRIM_HORIZON
                    };
                    var getShardIteratorResponse = await _streamsClient.GetShardIteratorAsync(getShardIteratorRequest);
                    var currentShardIterator = getShardIteratorResponse.ShardIterator;

                    var iterations = 0; // loop will continue for some time until the stream shard is closed, this just short circuits things for the test.
                    while (currentShardIterator != null && iterations < 10)
                    {
                        var getRecordsRequest = new GetRecordsRequest
                        {
                            ShardIterator = currentShardIterator,
                        };
                        var getRecordsResponse = await _streamsClient.GetRecordsAsync(getRecordsRequest);
                        foreach (var record in getRecordsResponse.Records)
                        {
                            _testOutputHelper.WriteLine($"{record.EventID} {record.EventName} {record.EventSource} {record.Dynamodb.NewImage.StreamViewType}");
                        }

                        currentShardIterator = getRecordsResponse.NextShardIterator;
                        iterations++;
                    }
                }

                lastEvaluatedShardId = describeStreamResponse.StreamDescription.LastEvaluatedShardId;

            } while (lastEvaluatedShardId != null);
        }

        public async Task InitializeAsync()
        {
            var listTablesResponse = await _client.ListTablesAsync();
            if (listTablesResponse.TableNames.Contains(OutboxTableName))
            {
                await _client.DeleteTableAsync(OutboxTableName);
            }

            var tableDescription = await GetTableStatus(OutboxTableName);
            while (tableDescription == null || tableDescription.TableStatus != "NOT FOUND")
            {
                await Task.Delay(100);
                tableDescription = await GetTableStatus(OutboxTableName);
            }

            if (listTablesResponse.TableNames.Contains(OrgsTableName))
            {
                await _client.DeleteTableAsync(OrgsTableName);
            }
            tableDescription = await GetTableStatus(OrgsTableName);
            while (tableDescription == null || tableDescription.TableStatus != "NOT FOUND")
            {
                await Task.Delay(100);
                tableDescription = await GetTableStatus(OrgsTableName);
            }

            var createOutBoxTableRequest = new CreateTableRequest
            {
                TableName = OutboxTableName,
                KeySchema = new List<KeySchemaElement>
                {
                    new KeySchemaElement("Id", KeyType.HASH),
                    new KeySchemaElement("Created", KeyType.RANGE)
                },
                AttributeDefinitions = new List<AttributeDefinition>
                {
                    new AttributeDefinition("Id", ScalarAttributeType.S),
                    new AttributeDefinition("Created", ScalarAttributeType.N),
                },
                BillingMode = BillingMode.PAY_PER_REQUEST,
                StreamSpecification = new StreamSpecification
                {
                    StreamEnabled = true,
                    StreamViewType = StreamViewType.NEW_IMAGE
                }
            };
            await _client.CreateTableAsync(createOutBoxTableRequest);
            var describeTableResponse = await _client.DescribeTableAsync(OutboxTableName);
            _tableLatestStreamArn = describeTableResponse.Table.LatestStreamArn;

            var updateTimeToLiveRequest = new UpdateTimeToLiveRequest
            {
                TableName = OutboxTableName,
                TimeToLiveSpecification = new TimeToLiveSpecification
                {
                    AttributeName = "Expiration", // Must be epoch seconds
                    Enabled = true
                }
            };
            await _client.UpdateTimeToLiveAsync(updateTimeToLiveRequest);

            var createOrgsTableRequest = new CreateTableRequest
            {
                TableName = OrgsTableName,
                KeySchema = new List<KeySchemaElement>
                {
                    new KeySchemaElement("Id", KeyType.HASH),
                    new KeySchemaElement("Name", KeyType.RANGE)
                },
                AttributeDefinitions = new List<AttributeDefinition>
                {
                    new AttributeDefinition("Id", ScalarAttributeType.N),
                    new AttributeDefinition("Name", ScalarAttributeType.S),
                },
                BillingMode = BillingMode.PAY_PER_REQUEST,
            };
            await _client.CreateTableAsync(createOrgsTableRequest);
        }

        public async Task DisposeAsync()
        {
            //await _client.DeleteTableAsync("Outbox");
        }

        public async Task<TableDescription> GetTableStatus(string tableName)
        {
            try
            {
                var response = await _client.DescribeTableAsync(new DescribeTableRequest
                {
                    TableName = tableName
                });
                return response.Table;
            }
            catch (ResourceNotFoundException)
            {
                return new TableDescription
                {
                    TableStatus = "NOT FOUND"
                };
            }
        }
    }
    
}
