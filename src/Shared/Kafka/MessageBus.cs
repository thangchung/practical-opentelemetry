using Confluent.Kafka;
using Google.Protobuf;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Shared.Kafka
{
    public class MessageBus
    {
        private readonly IConfiguration _config;
        private readonly ILogger<MessageBus> _logger;

        public MessageBus(IConfiguration config, ILogger<MessageBus> logger)
        {
            _config = config;
            _logger = logger;
        }

        public async Task PublishAsync<TMessage>(TMessage @event, Headers headers, string[] topics)
            where TMessage : IMessage<TMessage>
        {
            if (topics.Length <= 0) throw new Exception("Dispatch - Topic to publish should be at least one.");

            using var producer = new ProducerBuilder<Null, TMessage>(
                ConstructConfig(_config.GetValue<string>("Kafka:Connection")))
                .SetValueSerializer(new ProtoSerializer<TMessage>())
                .Build();

            foreach (var topic in topics)
            {
                await producer.ProduceAsync(topic, new Message<Null, TMessage> { Value = @event, Headers = headers ?? new Headers() });
                producer.Flush(TimeSpan.FromSeconds(10));
            }
        }

        public Task SubscribeAsync<TMessage>(Action<ConsumeResult<Null, TMessage>> callback, string[] topics, CancellationToken token)
            where TMessage : IMessage<TMessage>, new()
        {
            if (topics.Length <= 0)
                throw new Exception("Subscribe - Topics to subscribe should be at least one.");

            const int commitPeriod = 5;

            using var consumer = new ConsumerBuilder<Null, TMessage>(
                ConstructConfig(_config.GetValue<string>("Kafka:Connection")))
                .SetValueDeserializer(new ProtoDeserializer<TMessage>())
                .SetErrorHandler((_, e) => Console.WriteLine($"Error: {e.Reason}"))
                //.SetStatisticsHandler((_, json) => Console.WriteLine($"Statistics: {json}"))
                .Build();

            consumer.Subscribe(topics);

            _logger.LogInformation($"Subscribe - Subscribed to: [{string.Join(", ", consumer.Subscription)}]");

            try
            {
                while (true)
                {
                    try
                    {
                        var consumeResult = consumer.Consume(token);
                        callback(consumeResult);

                        if (consumeResult.IsPartitionEOF)
                        {
                            _logger.LogInformation(
                                $"Reached end of topic {consumeResult.Topic}, partition {consumeResult.Partition}, offset {consumeResult.Offset}.");

                            continue;
                        }

                        _logger.LogInformation($"Received message at {consumeResult.TopicPartitionOffset}: {consumeResult.Value}");

                        if (consumeResult.Offset % commitPeriod == 0)
                        {
                            // The Commit method sends a "commit offsets" request to the Kafka
                            // cluster and synchronously waits for the response. This is very
                            // slow compared to the rate at which the consumer is capable of
                            // consuming messages. A high performance application will typically
                            // commit offsets relatively infrequently and be designed handle
                            // duplicate messages in the event of failure.
                            try
                            {
                                consumer.Commit(consumeResult);
                            }
                            catch (KafkaException e)
                            {
                                _logger.LogError($"Commit error: {e.Error.Reason}");
                            }
                        }
                    }
                    catch (ConsumeException e)
                    {
                        _logger.LogError($"Consume error: {e.Error.Reason}");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogError("Closing consumer.");
                consumer.Close();
            }

            return Task.CompletedTask;
        }

        private static ConsumerConfig ConstructConfig(string brokerList)
        {
            return new ConsumerConfig
            {
                BootstrapServers = brokerList,
                GroupId = "coolstore-consumer",
                StatisticsIntervalMs = 5000,
                SessionTimeoutMs = 6000,
                EnableAutoCommit = false,
                AutoOffsetReset = AutoOffsetReset.Earliest,
                EnablePartitionEof = true
            };
        }
    }

    public class ProtoSerializer<T> : ISerializer<T> where T : IMessage<T>
    {
        public IEnumerable<KeyValuePair<string, object>>
            Configure(IEnumerable<KeyValuePair<string, object>> config, bool isKey)
        {
            return config;
        }

        public void Dispose()
        {
        }

        public byte[] Serialize(T data, SerializationContext context)
        {
            return data.ToByteArray();
        }
    }

    public class ProtoDeserializer<T> : IDeserializer<T>
        where T : IMessage<T>, new()
    {
        private readonly MessageParser<T> _parser;

        public ProtoDeserializer()
        {
            _parser = new MessageParser<T>(() => new T());
        }

        public IEnumerable<KeyValuePair<string, object>>
            Configure(IEnumerable<KeyValuePair<string, object>> config, bool isKey)
        {
            return config;
        }

        public void Dispose()
        {
        }

        public T Deserialize(ReadOnlySpan<byte> data, bool isNull, SerializationContext context)
        {
            return _parser.ParseFrom(data.ToArray());
        }
    }
}
