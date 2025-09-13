using System.ComponentModel.DataAnnotations;

namespace EnergyCalculator.Models;

public class AzureConfiguration
{
    public string SubscriptionId { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public string StorageConnectionString { get; set; } = string.Empty;
    public string KeyVaultUri { get; set; } = string.Empty;
    public AuthenticationConfiguration Authentication { get; set; } = new();
    public AutoDiscoveryConfiguration AutoDiscovery { get; set; } = new();
    public CostManagementConfiguration CostManagement { get; set; } = new();
    public List<MicroserviceConfiguration> Microservices { get; set; } = new();
    public List<string> SharedResources { get; set; } = new();
}

public class AuthenticationConfiguration
{
    public string Type { get; set; } = "DefaultAzureCredential"; // or "ServicePrincipal"
    public ServicePrincipalConfiguration ServicePrincipal { get; set; } = new();
}

public class ServicePrincipalConfiguration
{
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
}

public class AutoDiscoveryConfiguration
{
    public bool Enabled { get; set; } = true;
    public List<string> IncludeResourceTypes { get; set; } = new();
    public List<string> ExcludeResourceGroups { get; set; } = new();
    public Dictionary<string, string> MicroserviceNamingPatterns { get; set; } = new();
}

public class CostManagementConfiguration
{
    public bool Enabled { get; set; } = true;
    public bool UseRealData { get; set; } = false;
}

public class MicroserviceConfiguration
{
    public string Name { get; set; } = string.Empty;
    public string ResourceGroupName { get; set; } = string.Empty;
    public string AppServiceResourceId { get; set; } = string.Empty;
    public List<string> FunctionAppResourceIds { get; set; } = new();
    public List<string> ServiceBusResourceIds { get; set; } = new();
    public List<string> DatabaseResourceIds { get; set; } = new();
}
