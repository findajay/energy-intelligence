using Azure.Storage.Blobs;
using Azure.Data.Tables;
using EnergyCalculator.Models;
using System.Text.Json;

namespace EnergyCalculator.Services;

public interface IEnergyStorageService
{
    Task<string> SaveReportToBlob(object report);
    Task SaveReportToTable(EnergyReport report);
    Task<IEnumerable<EnergyReport>> GetReportHistory(DateTime startDate, DateTime endDate);
}

public class AzureStorageService : IEnergyStorageService
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly TableServiceClient _tableServiceClient;
    private const string ReportsContainer = "reports";
    private const string EnergyTable = "EnergyEstimates";

    public AzureStorageService(BlobServiceClient blobServiceClient, TableServiceClient tableServiceClient)
    {
        _blobServiceClient = blobServiceClient;
        _tableServiceClient = tableServiceClient;
    }

    public async Task<string> SaveReportToBlob(object report)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(ReportsContainer);
        await containerClient.CreateIfNotExistsAsync();

        var blobName = $"report-{DateTime.UtcNow:yyyy-MM-dd-HH-mm-ss}-{Guid.NewGuid()}.json";
        var blobClient = containerClient.GetBlobClient(blobName);

        var json = JsonSerializer.Serialize(report);
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));
        await blobClient.UploadAsync(stream, overwrite: true);

        return blobName;
    }

    public async Task SaveReportToTable(EnergyReport report)
    {
        var tableClient = _tableServiceClient.GetTableClient(EnergyTable);
        await tableClient.CreateIfNotExistsAsync();
        
        // Create a dictionary for Azure Tables that excludes the Details property
        var entityDict = new Dictionary<string, object>
        {
            ["PartitionKey"] = report.PartitionKey,
            ["RowKey"] = report.RowKey,
            ["KilowattHours"] = report.KilowattHours,
            ["CarbonKg"] = report.CarbonKg,
            ["ResourceType"] = report.ResourceType,
            ["ResourceName"] = report.ResourceName,
            ["UtilizationPercentage"] = report.UtilizationPercentage,
            ["DetailsJson"] = report.DetailsJson // Only store the JSON string
        };
        
        await tableClient.AddEntityAsync(new TableEntity(entityDict));
    }

    public async Task<IEnumerable<EnergyReport>> GetReportHistory(DateTime startDate, DateTime endDate)
    {
        var tableClient = _tableServiceClient.GetTableClient(EnergyTable);
        var filter = $"PartitionKey ge '{startDate:yyyy-MM-dd}' and PartitionKey le '{endDate:yyyy-MM-dd}'";
        
        var reports = new List<EnergyReport>();
        await foreach (var report in tableClient.QueryAsync<EnergyReport>(filter))
        {
            reports.Add(report);
        }

        return reports;
    }
}

// Mock storage service for testing (doesn't require Azure connection)
public class MockStorageService : IEnergyStorageService
{
    private readonly ILogger<MockStorageService> _logger;
    
    public MockStorageService(ILogger<MockStorageService> logger)
    {
        _logger = logger;
    }

    public async Task<string> SaveReportToBlob(object report)
    {
        _logger.LogInformation("Mock: Saving report to blob (simulated)");
        await Task.Delay(10); // Simulate async operation
        return $"mock-blob-{Guid.NewGuid()}.json";
    }

    public async Task SaveReportToTable(EnergyReport report)
    {
        _logger.LogInformation("Mock: Saving report to table (simulated) - {KWh} kWh", report.KilowattHours);
        await Task.Delay(10); // Simulate async operation
    }

    public async Task<IEnumerable<EnergyReport>> GetReportHistory(DateTime startDate, DateTime endDate)
    {
        _logger.LogInformation("Mock: Getting report history from {Start} to {End} (simulated)", startDate, endDate);
        await Task.Delay(10); // Simulate async operation
        return new List<EnergyReport>();
    }
}
