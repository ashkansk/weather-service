// See https://aka.ms/new-console-template for more information

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using StackExchange.Redis;
using WeatherService.Storage;
using WeatherService.Storage.Data;
using WeatherService.Storage.Services;

using IHost host = CreateHostBuilder(args).Build();
using var scope = host.Services.CreateScope();

try
{
    scope.ServiceProvider.GetRequiredService<App>().Run(args);
}
catch (Exception e)
{
    Console.WriteLine(e.Message);
}

IHostBuilder CreateHostBuilder(string[] strings)
{
    return Host.CreateDefaultBuilder()
        .ConfigureServices((_, services) =>
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json")
                .AddEnvironmentVariables()
                .AddCommandLine(args)
                .Build();
            
            services.AddSingleton<IConfiguration>(configuration);
            // Add connection multiplexer as singleton as mentioned in the docs
            services.AddSingleton<IConnectionMultiplexer>(provider =>
                {
                    var redisOptions = configuration.GetValue<string>("RedisConfig") ??
                                       throw new Exception("RedisConf should be present in app settings.");
                    return ConnectionMultiplexer.Connect(ConfigurationOptions.Parse(redisOptions));
                }
            );
            // Add IDatabase as singleton which is also thread-safe
            services.AddSingleton<IDatabase>(provider =>
            {
                var redis = provider.GetService<IConnectionMultiplexer>();
                return redis!.GetDatabase();
            });
            services.AddDbContext<WeatherDb>(options =>
                options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));
            services.AddScoped<WeatherInfoStorageService>();
            services.AddScoped<WeatherInfoFetchedConsumer>();
            services.AddScoped<App>();
        });
}