using Azure;
using Azure.Data.Tables;
using System.Text.Json;

namespace EnergyCalculator.Models;

public class EnergyReport : ITableEntity
{
    public string PartitionKey { get; set; } = DateTime.UtcNow.ToString("yyyy-MM-dd");
    public string RowKey { get; set; } = Guid.NewGuid().ToString();
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }
    
    public double KilowattHours { get; set; }
    public double CarbonKg { get; set; }
    public string ResourceType { get; set; } = string.Empty;
    public string ResourceName { get; set; } = string.Empty;
    public double UtilizationPercentage { get; set; }
    
    // Store dictionary as JSON string for Azure Tables compatibility
    public string DetailsJson { get; set; } = "{}";
    
    // Private backing field for the dictionary
    private Dictionary<string, double>? _details = null;
    
    // Property to work with Dictionary in code
    public Dictionary<string, double> Details 
    { 
        get 
        {
            if (_details == null)
            {
                _details = string.IsNullOrEmpty(DetailsJson) || DetailsJson == "{}" 
                    ? new Dictionary<string, double>() 
                    : JsonSerializer.Deserialize<Dictionary<string, double>>(DetailsJson) ?? new Dictionary<string, double>();
            }
            return _details;
        }
        set 
        {
            _details = value;
            DetailsJson = JsonSerializer.Serialize(value);
        }
    }
    
    // Call this method after modifying the Details dictionary to sync to JSON
    public void SyncDetailsToJson()
    {
        if (_details != null)
        {
            DetailsJson = JsonSerializer.Serialize(_details);
        }
    }
}
