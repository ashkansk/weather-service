using Confluent.Kafka;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using WeatherService;
using WeatherService.Data;
using WeatherService.Services;
using WeatherService.Services.Kafka;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var services = builder.Services;
services.AddControllers(options =>
{
    options.Conventions.Add(new RouteTokenTransformerConvention(new SlugifyParameterTransformer()));
});
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
services.AddEndpointsApiExplorer();
services.AddSwaggerGen();

services.AddDbContext<WeatherDb>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
// Add connection multiplexer as singleton as mentioned in the docs
services.AddSingleton<IConnectionMultiplexer>(provider =>
    {
        var redisOptions = builder.Configuration.GetValue<string>("RedisConfig") ??
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

services.AddSingleton<KafkaClientHandle>();
services.AddSingleton<KafkaDependentProducer<Null, string>>();

services.AddScoped<WeatherInfoService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();