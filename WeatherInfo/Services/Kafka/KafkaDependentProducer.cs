using Confluent.Kafka;

namespace WeatherInfo.Services.Kafka;

/// <summary>
/// Leverages the injected KafkaClientHandle instance to allow
/// Confluent.Kafka.Message{K,V}s to be produced to Kafka.
/// <br />
/// For more information see
/// <a href="https://github.com/confluentinc/confluent-kafka-dotnet/tree/master/examples/Web">this</a> GitHub
/// example from the library's own repository.
/// </summary>
public class KafkaDependentProducer<K, V>
{
    private readonly IProducer<K, V> kafkaHandle;

    public KafkaDependentProducer(KafkaClientHandle handle)
    {
        kafkaHandle = new DependentProducerBuilder<K, V>(handle.Handle).Build();
    }

    /// <summary>
    ///     Asynchronously produces a message and exposes delivery information
    ///     via the returned Task. Use this method of producing if you would
    ///     like to await the result before flow of execution continues.
    /// </summary>
    public Task ProduceAsync(string topic, Message<K, V> message)
        => this.kafkaHandle.ProduceAsync(topic, message);

    /// <summary>
    ///     Asynchronously produce a message and expose delivery information
    ///     via the provided callback function. Use this method of producing
    ///     if you would like flow of execution to continue immediately, and
    ///     handle delivery information out-of-band.
    /// </summary>
    public void Produce(string topic, Message<K, V> message, Action<DeliveryReport<K, V>>? deliveryHandler = null)
        => this.kafkaHandle.Produce(topic, message, deliveryHandler);

    public void Flush(TimeSpan timeout)
        => this.kafkaHandle.Flush(timeout);
}