using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Azure.Storage.Blobs;
using Azure.Data.Tables;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace EnergyCore;

public class CodeAnalyzer
{
    public async Task<double> AnalyzeAndStoreAsync(string repoPath, BlobServiceClient blobClient, TableServiceClient tableClient)
    {
        double totalWh = 0;
        foreach (var file in Directory.GetFiles(repoPath, "*.cs", SearchOption.AllDirectories))
        {
            var code = await File.ReadAllTextAsync(file);
            var tree = CSharpSyntaxTree.ParseText(code);
            var root = await tree.GetRootAsync();
            var loops = root.DescendantNodes().OfType<ForStatementSyntax>().Count();
            var nestedLoops = root.DescendantNodes().OfType<ForStatementSyntax>().Count(f => f.DescendantNodes().OfType<ForStatementSyntax>().Any());
            totalWh += loops * 0.01 + nestedLoops * 0.05;
        }
        var report = new { Wh = totalWh, Timestamp = DateTime.UtcNow };
        var blob = blobClient.GetBlobContainerClient("reports");
        await blob.CreateIfNotExistsAsync();
        await blob.UploadBlobAsync($"code-report-{Guid.NewGuid()}.json", new BinaryData(JsonSerializer.Serialize(report)));
        var table = tableClient.GetTableClient("EnergyEstimates");
        await table.CreateIfNotExistsAsync();
        await table.AddEntityAsync(new TableEntity(DateTime.UtcNow.ToString("yyyy-MM-dd"), Guid.NewGuid().ToString()) { { "Wh", totalWh } });
        return totalWh;
    }
}
