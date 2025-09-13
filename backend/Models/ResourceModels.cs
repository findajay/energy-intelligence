namespace EnergyCalculator.Models;

public class ResourceInfo
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string ResourceGroupName { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public Dictionary<string, string> Tags { get; set; } = new();
    public CostInfo? Cost { get; set; }
}

public class CostInfo
{
    public decimal DailyCost { get; set; }
    public decimal MonthlyCost { get; set; }
    public decimal YearlyEstimate { get; set; }
    public string Currency { get; set; } = "USD";
    public DateTime LastUpdated { get; set; }
    public List<CostBreakdown> Breakdown { get; set; } = new();
}

public class CostBreakdown
{
    public string Category { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string MeterName { get; set; } = string.Empty;
}

public class ResourceGroupInfo
{
    public string Name { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public Dictionary<string, string> Tags { get; set; } = new();
    public List<ResourceInfo> Resources { get; set; } = new();
    public CostInfo? TotalCost { get; set; }
}

public class MicroserviceInfo
{
    public string Name { get; set; } = string.Empty;
    public string ResourceGroupName { get; set; } = string.Empty;
    public List<ResourceInfo> AppServices { get; set; } = new();
    public List<ResourceInfo> FunctionApps { get; set; } = new();
    public List<ResourceInfo> ServiceBus { get; set; } = new();
    public List<ResourceInfo> Databases { get; set; } = new();
    public List<ResourceInfo> StorageAccounts { get; set; } = new();
    public List<ResourceInfo> Other { get; set; } = new();
    public CostInfo? TotalCost { get; set; }

    public bool HasResources =>
        AppServices.Any() || 
        FunctionApps.Any() || 
        ServiceBus.Any() || 
        Databases.Any() ||
        StorageAccounts.Any() ||
        Other.Any();

    public int TotalResourceCount =>
        AppServices.Count + 
        FunctionApps.Count + 
        ServiceBus.Count + 
        Databases.Count +
        StorageAccounts.Count +
        Other.Count;

    public decimal TotalMonthlyCost => TotalCost?.MonthlyCost ?? 0;
    public decimal TotalYearlyCost => TotalCost?.YearlyEstimate ?? 0;
}
