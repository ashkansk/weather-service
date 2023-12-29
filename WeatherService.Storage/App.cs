using WeatherService.Storage.Services;

namespace WeatherService.Storage;

public class App
{
    private readonly WeatherInfoFetchedConsumer weatherInfoFetchedConsumer;
    
    public App(WeatherInfoFetchedConsumer consumer)
    {
        weatherInfoFetchedConsumer = consumer;
    }

    public void Run(string[] args)
    {
        weatherInfoFetchedConsumer.Execute();
    }
}