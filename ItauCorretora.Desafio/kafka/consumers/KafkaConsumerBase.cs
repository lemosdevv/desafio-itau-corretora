using System.Text;
using Confluent.Kafka;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ItauCorretora.Desafio.Kafka.Consumers;

public abstract class KafkaConsumerBase<T> : BackgroundService where T : class
{
    private readonly IConsumer<Ignore, string> _consumer;
    protected readonly ILogger _logger;
    private readonly string _topic;

    protected KafkaConsumerBase(IConfiguration configuration, ILogger logger, string topic)
    {
        _logger = logger;
        _topic = topic;

        var consumerConfig = new ConsumerConfig
        {
            BootstrapServers = configuration["Kafka:BootstrapServers"],
            GroupId = $"{topic}-consumer-group",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false // We'll commit manually after processing
        };

        _consumer = new ConsumerBuilder<Ignore, string>(consumerConfig).Build();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _consumer.Subscribe(_topic);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var consumeResult = _consumer.Consume(stoppingToken);
                    
                    _logger.LogInformation("Message received from topic {Topic} at partition {Partition}, offset {Offset}", 
                        consumeResult.Topic, consumeResult.Partition, consumeResult.Offset);

                    await ProcessMessageAsync(consumeResult.Message.Value, stoppingToken);

                    // Commit after successful processing
                    _consumer.Commit(consumeResult);
                }
                catch (ConsumeException ex)
                {
                    _logger.LogError(ex, "Error consuming message from topic {Topic}", _topic);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing message from topic {Topic}", _topic);
                }
            }
        }
        finally
        {
            _consumer.Close();
        }
    }

    protected abstract Task ProcessMessageAsync(string message, CancellationToken stoppingToken);

    public override void Dispose()
    {
        _consumer?.Dispose();
        base.Dispose();
    }
}