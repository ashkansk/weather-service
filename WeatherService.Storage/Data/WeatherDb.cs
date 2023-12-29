using Microsoft.EntityFrameworkCore;
using WeatherService.Storage.Models;

namespace WeatherService.Storage.Data;

public class WeatherDb: DbContext
{
    public WeatherDb(DbContextOptions<WeatherDb> options): base(options)
    {
    }
    
    public DbSet<WeatherInfo> WeatherInfos { get; set; }
}