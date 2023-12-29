using Microsoft.AspNetCore.Mvc;
using WeatherService.Services;

namespace WeatherService.Controllers;

[ApiController]
[Route("[controller]")]
public class WeatherInfoController : ControllerBase
{
    private readonly ILogger<WeatherInfoController> logger;
    private readonly WeatherInfoService service;

    public WeatherInfoController(ILogger<WeatherInfoController> logger, WeatherInfoService service)
    {
        this.logger = logger;
        this.service = service;
    }


    [HttpGet]
    public async Task<string?> Get()
    {
        // Since the delay is approximately equal to the value provided, we set 100ms less than the actual client timeout
        // in order to ensure the request is processed in less than 5 seconds
        var clientTimeout = Task.Delay(49000000);
        logger.LogTrace("Request received.");
        var getLastInfoTask = service.GetLastInfoAsync();
        logger.LogTrace("Request processing started asynchronously.");
        var finishedTask = await Task.WhenAny(clientTimeout, getLastInfoTask);
        if (finishedTask == getLastInfoTask)
        {
            string? lastInfo = await getLastInfoTask;
            logger.LogTrace($"LastInfo fetched: {lastInfo}");
            return lastInfo;
        }

        // else
        logger.LogWarning("Timeout reached, the request was not processed withing the required timeframe.");
        return null;
    }
}