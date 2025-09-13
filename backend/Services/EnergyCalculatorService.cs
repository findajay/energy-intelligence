using System.Diagnostics;
using System.Text.Json;
using EnergyCalculator.Models;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using EnergyCore;

namespace EnergyCalculator.Services;

public interface IEnergyCalculatorService
{
    Task<double> CalculateInfrastructureEnergy(string terraformJson);
    Task<double> CalculateCodeEnergy(string sourceCode);
    Task<AzureResourceAnalyzer.ResourceEnergyReport> CalculateAzureResourceEnergy(string functionAppResourceId, string serviceBusResourceId, string mongoDbResourceId, DateTime startTime, DateTime endTime);
}

public class EnergyCalculatorService : IEnergyCalculatorService
{
    private readonly AzureResourceAnalyzer _azureResourceAnalyzer;
    
    public EnergyCalculatorService(IConfiguration configuration)
    {
        _azureResourceAnalyzer = new AzureResourceAnalyzer(
            configuration.GetValue<string>("Azure:SubscriptionId"));
    }

    private static readonly Dictionary<string, (double BaseWattage, double MaxWattage)> VmPowerProfiles = new()
    {
        { "Standard_D2_v3", (50, 100) },
        { "Standard_D4_v3", (100, 200) },
        { "Standard_D8_v3", (200, 400) }
    };

    public async Task<double> CalculateInfrastructureEnergy(string terraformJson)
    {
        var totalKwh = 0.0;
        var jsonDoc = JsonDocument.Parse(terraformJson);

        foreach (var resource in jsonDoc.RootElement.GetProperty("resources").EnumerateArray())
        {
            if (resource.GetProperty("type").GetString() == "azurerm_virtual_machine")
            {
                var vmSize = resource.GetProperty("size").GetString();
                if (VmPowerProfiles.TryGetValue(vmSize ?? "", out var profile))
                {
                    // Calculate for 24 hours at 50% utilization
                    var avgWatts = (profile.BaseWattage + profile.MaxWattage) / 2;
                    var kwh = (avgWatts * 24 * 0.5) / 1000;
                    totalKwh += kwh;
                }
            }
        }

        return totalKwh;
    }

    public async Task<double> CalculateCodeEnergy(string sourceCode)
    {
        var totalKwh = 0.0;
        var tree = CSharpSyntaxTree.ParseText(sourceCode);
        var root = await tree.GetRootAsync();

        // Count compute-intensive patterns
        var loops = root.DescendantNodes()
            .OfType<ForStatementSyntax>()
            .Count();
        
        var nestedLoops = root.DescendantNodes()
            .OfType<ForStatementSyntax>()
            .Count(f => f.DescendantNodes().OfType<ForStatementSyntax>().Any());

        // Rough estimation: 0.01 kWh per loop, 0.05 kWh per nested loop
        totalKwh += loops * 0.01;
        totalKwh += nestedLoops * 0.05;

        return totalKwh;
    }

    public async Task<AzureResourceAnalyzer.ResourceEnergyReport> CalculateAzureResourceEnergy(
        string functionAppResourceId,
        string serviceBusResourceId,
        string mongoDbResourceId,
        DateTime startTime,
        DateTime endTime)
    {
        return await _azureResourceAnalyzer.GenerateCompleteEnergyReport(
            functionAppResourceId,
            serviceBusResourceId,
            mongoDbResourceId,
            startTime,
            endTime);
    }
}
