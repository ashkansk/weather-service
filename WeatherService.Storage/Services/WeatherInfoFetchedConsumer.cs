using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using WeatherService.Storage.Models;

namespace WeatherService.Storage.Services;

public class WeatherInfoFetchedConsumer
{
    private readonly ILogger<WeatherInfoFetchedConsumer> logger;
    private readonly WeatherInfoStorageService storageService;
    private readonly IConsumer<Ignore, string> consumer;
    private const string LastInfoRedisKey = "LastInfo";

    public WeatherInfoFetchedConsumer(
        IConfiguration configuration,
        ILogger<WeatherInfoFetchedConsumer> logger,
        WeatherInfoStorageService storageService
    )
    {
        this.logger = logger;
        this.storageService = storageService;

        var consumerConfig = new ConsumerConfig()
        {
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = true
        };
        configuration.GetSection("Kafka:Consumer").Bind(consumerConfig);

        consumer = new ConsumerBuilder<Ignore, string>(consumerConfig).Build();
        var weatherInfoFetchedTopic = configuration.GetValue<string>("Kafka:WeatherInfoFetchedTopic") ??
                                      throw new Exception("Kafka topic related configuration not found.");
        consumer.Subscribe(weatherInfoFetchedTopic);
    }

    public void Execute()
    {
        try
        {
            while (true)
            {
                try
                {
                    var consumeResult = consumer.Consume(CancellationToken.None);

                    if (consumeResult.IsPartitionEOF)
                    {
                        logger.LogInformation(
                            $"Reached end of topic {consumeResult.Topic}, partition {consumeResult.Partition}, offset {consumeResult.Offset}."
                        );
                        continue;
                    }

                    logger.LogTrace(
                        $"Received message at {consumeResult.TopicPartitionOffset}: {consumeResult.Message.Value}");
                    var weatherInfo = JsonSerializer.Deserialize<WeatherInfo>(consumeResult.Message.Value)!;
                    storageService.SaveWeatherInfo(weatherInfo);
                    try
                    {
                        // Store the offset associated with consumeResult to a local cache. Stored offsets are committed to Kafka by a background thread every AutoCommitIntervalMs. 
                        // The offset stored is actually the offset of the consumeResult + 1 since by convention, committed offsets specify the next message to consume. 
                        // If EnableAutoOffsetStore had been set to the default value true, the .NET client would automatically store offsets immediately prior to delivering messages to the application. 
                        // Explicitly storing offsets after processing gives at-least once semantics, the default behavior does not.
                        consumer.StoreOffset(consumeResult);
                    }
                    catch (KafkaException e)
                    {
                        logger.LogError($"Store Offset error: {e.Error.Reason}");
                    }
                }
                catch (ConsumeException e)
                {
                    logger.LogError($"Consume error: {e.Error.Reason}");
                }
            }
        }
        catch (OperationCanceledException)
        {
            logger.LogError("Closing consumer.");
            consumer.Close();
        }
    }
}