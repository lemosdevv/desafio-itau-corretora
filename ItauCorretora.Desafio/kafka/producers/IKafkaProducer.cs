using Confluent.Kafka;

namespace ItauCorretora.Desafio.Kafka.Producers;

public interface IKafkaProducer
{
    Task ProduceAsync<T>(string topic, T message);
}

public class KafkaProducer : IKafkaProducer
{
    private readonly IProducer<Null, string> _producer;
    private readonly ILogger<KafkaProducer> _logger;

    public KafkaProducer(IConfiguration configuration, ILogger<KafkaProducer> logger)
    {
        _logger = logger;
        var config = new ProducerConfig
        {
            BootstrapServers = configuration["Kafka:BootstrapServers"]
        };
        _producer = new ProducerBuilder<Null, string>(config).Build();
    }

    public async Task ProduceAsync<T>(string topic, T message)
    {
        try
        {
            var jsonMessage = System.Text.Json.JsonSerializer.Serialize(message);
            var kafkaMessage = new Message<Null, string> { Value = jsonMessage };
            
            var deliveryResult = await _producer.ProduceAsync(topic, kafkaMessage);
            
            _logger.LogInformation(
                "Mensagem entregue ao tópico {Topic} na partição {Partition}, offset {Offset}",
                deliveryResult.Topic, deliveryResult.Partition, deliveryResult.Offset);
        }
        catch (ProduceException<Null, string> ex)
        {
            _logger.LogError(ex, "Erro ao produzir mensagem para o tópico {Topic}", topic);
            throw;
        }
    }
}