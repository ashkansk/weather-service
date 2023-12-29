using Confluent.Kafka;
using StackExchange.Redis;
using WeatherInfo.Services.Kafka;

namespace WeatherInfo.Services;

/// <summary>
/// Connects to and fetches different information from the weather info web service and stores it in the cache alongside
/// persisting in a relation DB.
/// </summary>
public class WeatherInfoService
{
    private readonly IDatabase redisCache;

    /// <summary>
    /// Using `Null` key indicates using random partitioner. That's because we don't care about the order of events
    /// in the `weatherInfoFetchedTopic`, we just need them to be persisted in the relational DB. The order of the
    /// persisted records are determined by the records timestamp.
    /// </summary>
    private readonly KafkaDependentProducer<Null, string> producer;

    private readonly ILogger<WeatherInfoService> logger;
    private readonly string weatherInfoWebServiceUrl;
    private const string WeatherInfoWebServiceUrlConfigKey = "WeatherInfoWebServiceUrl";
    private readonly string weatherInfoFetchedTopic;
    private const string LastInfoRedisKey = "LastInfo";

    private static readonly HttpClient HttpClient = new()
    {
        // Setting the timeout once because we won't change it later. No need for app settings.
        Timeout = TimeSpan.FromSeconds(4)
    };

    public WeatherInfoService(
        IConfiguration configuration,
        IDatabase redisCache,
        KafkaDependentProducer<Null, string> producer,
        ILogger<WeatherInfoService> logger
    )
    {
        weatherInfoWebServiceUrl =
            configuration.GetValue<string>(WeatherInfoWebServiceUrlConfigKey) ??
            throw new Exception($"{WeatherInfoWebServiceUrlConfigKey} configuration is not present");
        weatherInfoFetchedTopic = configuration.GetValue<string>("Kafka:WeatherInfoFetchedTopic") ??
                                  throw new Exception("Kafka topic related configuration not found.");
        this.redisCache = redisCache;
        this.producer = producer;
        this.logger = logger;
    }

    /// <summary>
    /// Gets the last weather information available either via the weather web service or using the previously cached
    /// or persisted value.
    /// The process includes calling the weather web service with a timeout of 4 seconds.
    /// 1. If the response is successfully fetched, then the "LastInfo" data would be updated in Redis and
    /// an event (message) is put into Kafka for saving the data in MS SQL Server and finally the result is returned.
    /// 2. If the request to web service fails somehow (timeout, network issues, etc.) then it tries to read the
    /// "LastInfo" data from Redis with timeout of 500ms. If the value of "LastInfo" is successfully fetched from Redis
    /// then we return it.
    /// 3. If none of above works, then `null` is returned.
    /// 
    /// </summary>
    /// <returns>The weather info as a JSON string.</returns>
    public async Task<string?> GetLastInfoAsync()
    {
        HttpClient.Timeout = TimeSpan.FromSeconds(3);
        string? lastInfo = null;
        try
        {
            var response = await HttpClient.GetAsync(new Uri(weatherInfoWebServiceUrl, UriKind.Absolute));
            lastInfo = await response.Content.ReadAsStringAsync();

            // Put the newly fetched info into Redis cache (no await because we don't want the result)
            redisCache.StringSetAsync(LastInfoRedisKey, lastInfo);

            // Inform about the newly fetched info
            var infoFetchedEvent = new Message<Null, string> { Value = lastInfo };
            // no need to await this method since we don't need the result
            producer.Produce(weatherInfoFetchedTopic, infoFetchedEvent, DeliveryReportHandler);
        }
        catch (Exception e)
        {
            logger.LogWarning("Error occurred while fetching and saving the weather info: {0}", e);
            // Do nothing, `lastInfo` will be null
        }

        // try to get last available info from the Redis cache
        if (lastInfo == null)
        {
            try
            {
                var cachedInfo = await redisCache.StringGetAsync(LastInfoRedisKey);
                if(cachedInfo.HasValue)
                    lastInfo = cachedInfo.ToString();
            }
            catch (Exception e)
            {
                logger.LogWarning("Error occurred while fetching last info from cache: {0}", e);
                // Do nothing, `lastInfo` will be null
            }
        }

        // As a last resort try to fetch data from the relational database
        // (in case of Redis losing its cached data due to power failure or etc.)
        if (lastInfo == null)
        {
            try
            {
                // TODO: get from repository
            }
            catch (Exception e)
            {
                logger.LogWarning("Error occurred while fetching last info from cache: {0}", e);
                // Do nothing, `lastInfo` will be null
            }
        }

        return lastInfo;
    }

    private void DeliveryReportHandler(DeliveryReport<Null, string> deliveryReport)
    {
        // Log if the message delivery failed
        if (deliveryReport.Status == PersistenceStatus.NotPersisted)
        {
            logger.Log(LogLevel.Warning, $"Message delivery failed: {deliveryReport.Message.Value}");
        }
    }
}