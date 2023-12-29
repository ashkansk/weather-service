namespace WeatherService.Models;

/// <summary>
/// Holds information of a single call to the weather web service with the timestamp of the request.
/// Note that only a single record will be stored in the database indicating the last info.
/// </summary>
public class WeatherInfo
{
    /// <summary>
    /// The unique identifier of the entity. Id will always be set to 1 since we have only a single record.
    /// </summary>
    public int Id { get; set; }
    /// <summary>
    /// The timestamp of the request.
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// The raw response fetched from the weather web service.
    /// </summary>
    public String Info { get; set; }
}
