using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.Resources;
using EnergyCalculator.Models;
using Microsoft.Extensions.Options;

namespace EnergyCalculator.Services;

public interface IAzureResourceDiscoveryService
{
    Task<List<MicroserviceInfo>> DiscoverMicroservicesAsync();
    Task<List<ResourceInfo>> DiscoverSharedResourcesAsync();
    Task<List<ResourceGroupInfo>> DiscoverAllResourceGroupsAsync();
    Task<List<ResourceInfo>> DiscoverAllResourcesAsync();
    Task<bool> TestConnectionAsync();
}

public class AzureResourceDiscoveryService : IAzureResourceDiscoveryService
{
    private readonly ArmClient _armClient;
    private readonly AzureConfiguration _azureConfig;
    private readonly ILogger<AzureResourceDiscoveryService> _logger;

    // Resource types that typically belong to microservices
    private readonly HashSet<string> _microserviceResourceTypes = new()
    {
        "Microsoft.Web/sites",
        "Microsoft.Web/serverFarms",
        "Microsoft.ServiceBus/namespaces",
        "Microsoft.Sql/servers",
        "Microsoft.DocumentDB/databaseAccounts",
        "Microsoft.Storage/storageAccounts",
        "Microsoft.Cache/Redis",
        "Microsoft.KeyVault/vaults"
    };

    // Resource types that are typically shared infrastructure
    private readonly HashSet<string> _sharedResourceTypes = new()
    {
        "Microsoft.Network/virtualNetworks",
        "Microsoft.Network/networkSecurityGroups",
        "Microsoft.Network/publicIPAddresses",
        "Microsoft.Network/loadBalancers",
        "Microsoft.Insights/components",
        "Microsoft.AppConfiguration/configurationStores",
        "Microsoft.ContainerRegistry/registries",
        "Microsoft.OperationalInsights/workspaces"
    };

    public AzureResourceDiscoveryService(IOptions<AzureConfiguration> azureConfig, ILogger<AzureResourceDiscoveryService> logger)
    {
        _azureConfig = azureConfig.Value;
        _logger = logger;

        // Create credential based on configuration
        TokenCredential credential = _azureConfig.Authentication.Type switch
        {
            "ServicePrincipal" => new ClientSecretCredential(
                _azureConfig.TenantId,
                _azureConfig.Authentication.ServicePrincipal.ClientId,
                _azureConfig.Authentication.ServicePrincipal.ClientSecret),
            _ => new DefaultAzureCredential()
        };

        _armClient = new ArmClient(credential);
    }

    public async Task<bool> TestConnectionAsync()
    {
        try
        {
            var subscription = _armClient.GetSubscriptionResource(
                SubscriptionResource.CreateResourceIdentifier(_azureConfig.SubscriptionId));
            var subscriptionData = await subscription.GetAsync();
            _logger.LogInformation($"Successfully connected to Azure subscription: {subscriptionData.Value.Data.DisplayName}");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to Azure");
            return false;
        }
    }

    public async Task<List<ResourceInfo>> DiscoverAllResourcesAsync()
    {
        try
        {
            var resources = new List<ResourceInfo>();
            var subscription = _armClient.GetSubscriptionResource(
                SubscriptionResource.CreateResourceIdentifier(_azureConfig.SubscriptionId));

            await foreach (var resource in subscription.GetGenericResourcesAsync())
            {
                try
                {
                    var resourceInfo = new ResourceInfo
                    {
                        Id = resource.Id.ToString(),
                        Name = resource.Data.Name,
                        Type = resource.Data.ResourceType.ToString(),
                        ResourceGroupName = ExtractResourceGroupFromId(resource.Id.ToString()),
                        Location = resource.Data.Location.DisplayName ?? "Unknown",
                        Tags = resource.Data.Tags?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value) ?? new Dictionary<string, string>()
                    };

                    resources.Add(resourceInfo);
                    _logger.LogDebug($"Discovered resource: {resourceInfo.Name} ({resourceInfo.Type})");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to process resource {ResourceId}", resource.Id);
                }
            }

            _logger.LogInformation($"Discovered {resources.Count} resources in subscription");
            return resources;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error discovering Azure resources");
            throw;
        }
    }

    public async Task<List<ResourceGroupInfo>> DiscoverAllResourceGroupsAsync()
    {
        try
        {
            var resourceGroups = new List<ResourceGroupInfo>();
            var subscription = _armClient.GetSubscriptionResource(
                SubscriptionResource.CreateResourceIdentifier(_azureConfig.SubscriptionId));

            await foreach (var resourceGroup in subscription.GetResourceGroups().GetAllAsync())
            {
                var rgInfo = new ResourceGroupInfo
                {
                    Name = resourceGroup.Data.Name,
                    Location = resourceGroup.Data.Location.ToString(),
                    Tags = resourceGroup.Data.Tags?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value) ?? new Dictionary<string, string>(),
                    Resources = new List<ResourceInfo>()
                };

                // Get all resources in this resource group
                await foreach (var resource in resourceGroup.GetGenericResourcesAsync())
                {
                    var resourceInfo = new ResourceInfo
                    {
                        Id = resource.Id.ToString(),
                        Name = resource.Data.Name,
                        Type = resource.Data.ResourceType.ToString(),
                        ResourceGroupName = resourceGroup.Data.Name,
                        Location = resource.Data.Location.DisplayName ?? rgInfo.Location,
                        Tags = resource.Data.Tags?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value) ?? new Dictionary<string, string>()
                    };

                    rgInfo.Resources.Add(resourceInfo);
                }

                resourceGroups.Add(rgInfo);
                _logger.LogDebug($"Discovered resource group: {rgInfo.Name} with {rgInfo.Resources.Count} resources");
            }

            _logger.LogInformation($"Discovered {resourceGroups.Count} resource groups");
            return resourceGroups;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error discovering resource groups");
            throw;
        }
    }

    public async Task<List<MicroserviceInfo>> DiscoverMicroservicesAsync()
    {
        try
        {
            var allResourceGroups = await DiscoverAllResourceGroupsAsync();
            var microservices = new List<MicroserviceInfo>();

            foreach (var resourceGroup in allResourceGroups)
            {
                // Skip resource groups that are clearly shared infrastructure
                if (IsSharedInfrastructureResourceGroup(resourceGroup.Name))
                    continue;

                // NEW LOGIC: Only consider resource groups with at least one App Service as microservices
                var appServices = resourceGroup.Resources
                    .Where(r => r.Type.Equals("Microsoft.Web/sites", StringComparison.OrdinalIgnoreCase) && !IsFunctionApp(r))
                    .ToList();

                // If no App Services found, this is not a microservice - skip it
                if (!appServices.Any())
                {
                    _logger.LogDebug($"Resource group {resourceGroup.Name} skipped - no App Services found");
                    continue;
                }

                // This resource group qualifies as a microservice
                var microserviceResources = resourceGroup.Resources
                    .Where(r => _microserviceResourceTypes.Contains(r.Type))
                    .ToList();

                var microservice = new MicroserviceInfo
                {
                    Name = ExtractMicroserviceName(resourceGroup.Name),
                    ResourceGroupName = resourceGroup.Name,
                    AppServices = new List<ResourceInfo>(),
                    FunctionApps = new List<ResourceInfo>(),
                    ServiceBus = new List<ResourceInfo>(),
                    Databases = new List<ResourceInfo>(),
                    StorageAccounts = new List<ResourceInfo>(),
                    Other = new List<ResourceInfo>()
                };

                // Categorize resources by type
                foreach (var resource in microserviceResources)
                {
                    switch (resource.Type.ToLower())
                    {
                        case "microsoft.web/sites":
                            if (IsFunctionApp(resource))
                                microservice.FunctionApps.Add(resource);
                            else
                                microservice.AppServices.Add(resource);
                            break;
                        case "microsoft.servicebus/namespaces":
                            microservice.ServiceBus.Add(resource);
                            break;
                        case "microsoft.sql/servers":
                        case "microsoft.documentdb/databaseaccounts":
                            microservice.Databases.Add(resource);
                            break;
                        case "microsoft.storage/storageaccounts":
                            microservice.StorageAccounts.Add(resource);
                            break;
                        default:
                            microservice.Other.Add(resource);
                            break;
                    }
                }

                microservices.Add(microservice);
                _logger.LogInformation($"Qualified microservice: {microservice.Name} in resource group: {microservice.ResourceGroupName} (has {microservice.AppServices.Count} App Services)");
            }

            _logger.LogInformation($"Discovered {microservices.Count} microservices (resource groups with App Services)");
            return microservices;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error discovering microservices");
            throw;
        }
    }

    public async Task<List<ResourceInfo>> DiscoverSharedResourcesAsync()
    {
        try
        {
            var allResources = await DiscoverAllResourcesAsync();
            var sharedResources = allResources
                .Where(r => _sharedResourceTypes.Contains(r.Type) || 
                           IsSharedInfrastructureResourceGroup(r.ResourceGroupName))
                .ToList();

            _logger.LogInformation($"Discovered {sharedResources.Count} shared resources");
            return sharedResources;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error discovering shared resources");
            throw;
        }
    }

    private string ExtractResourceGroupFromId(string resourceId)
    {
        var parts = resourceId.Split('/');
        var rgIndex = Array.IndexOf(parts, "resourceGroups");
        return rgIndex >= 0 && rgIndex + 1 < parts.Length ? parts[rgIndex + 1] : "Unknown";
    }

    private string ExtractMicroserviceName(string resourceGroupName)
    {
        // Extract microservice name from resource group name
        // Common patterns: "myapp-dev-servicename", "rg-servicename-dev", "servicename-resourcegroup"
        var cleanName = resourceGroupName.ToLower()
            .Replace("rg-", "")
            .Replace("-rg", "")
            .Replace("resourcegroup", "")
            .Replace("resource-group", "");

        // Try to identify the service name part
        var parts = cleanName.Split('-', '_');
        
        // Common environment suffixes to remove
        var envSuffixes = new[] { "dev", "test", "staging", "prod", "production", "qa", "uat" };
        var serviceParts = parts.Where(p => !envSuffixes.Contains(p.ToLower())).ToArray();
        
        return serviceParts.LastOrDefault() ?? resourceGroupName;
    }

    private bool IsSharedInfrastructureResourceGroup(string resourceGroupName)
    {
        var name = resourceGroupName.ToLower();
        var sharedIndicators = new[]
        {
            "shared", "common", "infrastructure", "infra", "network", "networking", 
            "security", "monitoring", "logs", "insights", "management", "ops"
        };

        return sharedIndicators.Any(indicator => name.Contains(indicator));
    }

    private bool IsFunctionApp(ResourceInfo resource)
    {
        if (resource.Type.ToLower() != "microsoft.web/sites")
            return false;

        // Check naming patterns that indicate function apps
        var functionKeywords = new[] { "func", "function", "worker", "processor", "handler", "trigger" };
        var name = resource.Name.ToLower();
        
        // Check if name contains function app indicators
        if (functionKeywords.Any(keyword => name.Contains(keyword)))
            return true;

        // Check tags for function app indicators
        if (resource.Tags != null)
        {
            foreach (var tag in resource.Tags)
            {
                var key = tag.Key.ToLower();
                var value = tag.Value.ToLower();
                
                if (key.Contains("function") || key.Contains("worker") || 
                    value.Contains("function") || value.Contains("worker"))
                    return true;
            }
        }

        // Additional heuristics: if the resource is named like a microservice but with specific suffixes
        // that often indicate background processing (which is common for function apps)
        var processingIndicators = new[] { "temp", "process", "batch", "job", "queue", "event" };
        if (processingIndicators.Any(indicator => name.Contains(indicator)))
            return true;

        return false;
    }
}
