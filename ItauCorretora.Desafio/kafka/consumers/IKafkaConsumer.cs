namespace ItauCorretora.Desafio.Kafka.Consumers;

public interface IKafkaConsumer
{
    Task StartConsumingAsync(CancellationToken cancellationToken);
    void StopConsuming();
}