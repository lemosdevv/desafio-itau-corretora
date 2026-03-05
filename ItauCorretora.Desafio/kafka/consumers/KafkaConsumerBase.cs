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
    private bool _isEnabled;

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
    if (!_isEnabled)
    {
        _logger.LogWarning("Kafka consumer disabled for the topic {Topic}", _topic);
        return;
    }

    try
    {
        _consumer.Subscribe(_topic);
        _logger.LogInformation("Kafka consumer subscribed to the topic {Topic}", _topic);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var consumeResult = _consumer.Consume(stoppingToken);
                _logger.LogInformation("Message received from topic {Topic}...", consumeResult.Topic);
                await ProcessMessageAsync(consumeResult.Message.Value, stoppingToken);
                _consumer.Commit(consumeResult);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Consumption canceled for the topic {Topic}", _topic);
                break;
            }
            catch (ConsumeException ex)
            {
                _logger.LogError(ex, "Error occurred while consuming message from topic {Topic}", _topic);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while processing message from topic {Topic}", _topic);
            }
        }
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Fatal error in Kafka consumer for the topic {Topic}", _topic);
    }
    finally
    {
        _consumer?.Close();
        _logger.LogInformation("Kafka consumer for the topic {Topic} finalized.", _topic);
    }
}

    protected abstract Task ProcessMessageAsync(string message, CancellationToken stoppingToken);

    public override void Dispose()
    {
        _consumer?.Dispose();
        base.Dispose();
    }
}