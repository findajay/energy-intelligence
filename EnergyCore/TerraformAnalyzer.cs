using System.Diagnostics;
using System.Text.Json;
using Azure.Storage.Blobs;
using Azure.Data.Tables;

namespace EnergyCore;

public class TerraformAnalyzer
{
    public async Task<double> AnalyzeAndStoreAsync(string terraformPath, BlobServiceClient blobClient, TableServiceClient tableClient, double utilization = 0.5)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "terraform",
                Arguments = "show -json",
                RedirectStandardOutput = true,
                WorkingDirectory = terraformPath
            }
        };
        process.Start();
        var json = await process.StandardOutput.ReadToEndAsync();
        process.WaitForExit();
        var doc = JsonDocument.Parse(json);
        double totalKWh = 0;
        foreach (var resource in doc.RootElement.GetProperty("resources").EnumerateArray())
        {
            if (resource.GetProperty("type").GetString() == "azurerm_virtual_machine")
            {
                var count = resource.GetProperty("instances").GetArrayLength();
                var size = resource.GetProperty("values").GetProperty("vm_size").GetString();
                double power = size == "Standard_D2_v3" ? 100 : 50; // Example profile
                totalKWh += count * power * 24 * utilization / 1000;
            }
        }
        var report = new { kWh = totalKWh, Timestamp = DateTime.UtcNow };
        var blob = blobClient.GetBlobContainerClient("reports");
        await blob.CreateIfNotExistsAsync();
        await blob.UploadBlobAsync($"report-{Guid.NewGuid()}.json", new BinaryData(JsonSerializer.Serialize(report)));
        var table = tableClient.GetTableClient("EnergyEstimates");
        await table.CreateIfNotExistsAsync();
        await table.AddEntityAsync(new TableEntity(DateTime.UtcNow.ToString("yyyy-MM-dd"), Guid.NewGuid().ToString()) { { "kWh", totalKWh } });
        return totalKWh;
    }
}
