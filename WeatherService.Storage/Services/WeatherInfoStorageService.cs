using System.Text.Json;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using WeatherService.Storage.Data;
using WeatherService.Storage.Models;

namespace WeatherService.Storage.Services;

/// <summary>
/// The service class for saving the weather info into the persistent storage.
/// </summary>
public class WeatherInfoStorageService
{
    private readonly WeatherDb db;
    private readonly IDatabase redisCache;
    private readonly ILogger<WeatherInfoStorageService> logger;
    private const string LastInfoRedisKey = "LastInfo";


    public WeatherInfoStorageService(WeatherDb db, IDatabase redisCache, ILogger<WeatherInfoStorageService> logger)
    {
        this.db = db;
        this.redisCache = redisCache;
        this.logger = logger;
    }

    /// <summary>
    /// First checks if the info received is up-to-date by comparing it with the cached value in Redis.
    /// This way we skip many values in case of high request rates and prevent over-loading the SQL Server with read and
    /// write request. We only write the values which are new.
    /// </summary>
    /// <param name="weatherInfo"></param>
    public void SaveWeatherInfo(WeatherInfo weatherInfo)
    {
        WeatherInfo? cachedLastInfo = null;
        try
        {
            var redisValue = redisCache.StringGet(LastInfoRedisKey);
            if (redisValue.HasValue)
                cachedLastInfo = JsonSerializer.Deserialize<WeatherInfo>(redisValue.ToString())!;
        }
        catch (Exception e)
        {
            logger.LogError($"Error getting value from Redis: {e.Message}");
        }

        if (cachedLastInfo == null)
        {
            // If there's no info present in cache, then the data should be compared with the value in DB
            UpdateDbRecordIfNotOlder(weatherInfo);
        }
        else if (weatherInfo.Timestamp >= cachedLastInfo.Timestamp)
        {
            UpdateDbRecordIfNotOlder(weatherInfo);
            // ELSE: skip this record and proceed to the next
            /*
             * Note that no starvation occurs here. Meaning that Redis will always contain the newest data, so the
             * data would never be written to the SQL Server. This is a valid argument, but there's no problem
             * as long as Redis is up and running as expected and can provide the latest data in case of the weather
             * web service is not responding. If in rare cases Redis would not respond, then we have all the values
             * in Kafka queued.
             */
        }
    }

    private void UpdateDbRecordIfNotOlder(WeatherInfo weatherInfo)
    {
        var dbWeatherInfo = db.WeatherInfos.SingleOrDefault(e => e.Id == 1);
        if (dbWeatherInfo == null)
        {
            weatherInfo.Id = 1;
            db.WeatherInfos.Add(weatherInfo);
            db.SaveChanges();
        }
        else if (weatherInfo.Timestamp > dbWeatherInfo.Timestamp)
        {
            dbWeatherInfo.Timestamp = weatherInfo.Timestamp;
            dbWeatherInfo.Info = weatherInfo.Info;
            db.SaveChanges();
        }
    }
}