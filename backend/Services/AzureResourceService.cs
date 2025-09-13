using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.Resources;
using Azure.Monitor.Query;
using EnergyCalculator.Models;
using Microsoft.Extensions.Options;

namespace EnergyCalculator.Services;

public interface IAzureResourceService
{
    Task<List<MicroserviceInfo>> GetMicroservicesAsync();
    Task<List<ResourceInfo>> GetSharedResourcesAsync();
    Task<bool> TestConnectionAsync();
}

public class AzureResourceService : IAzureResourceService
{
    private readonly ArmClient _armClient;
    private readonly MetricsQueryClient _metricsClient;
    private readonly AzureConfiguration _azureConfig;
    private readonly IAzureResourceDiscoveryService _discoveryService;
    private readonly IAzureCostService _costService;
    private readonly ILogger<AzureResourceService> _logger;

    public AzureResourceService(
        IOptions<AzureConfiguration> azureConfig, 
        IAzureResourceDiscoveryService discoveryService,
        IAzureCostService costService,
        ILogger<AzureResourceService> logger)
    {
        _azureConfig = azureConfig.Value;
        _discoveryService = discoveryService;
        _costService = costService;
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
        _metricsClient = new MetricsQueryClient(credential);
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

    public async Task<List<MicroserviceInfo>> GetMicroservicesAsync()
    {
        try
        {
            List<MicroserviceInfo> microservices;

            // If microservices are manually configured, use those
            if (_azureConfig.Microservices?.Any() == true)
            {
                _logger.LogInformation("Using manually configured microservices");
                microservices = await GetManuallyConfiguredMicroservicesAsync();
            }
            else
            {
                // Otherwise, use auto-discovery
                _logger.LogInformation("No manual configuration found, using auto-discovery");
                microservices = await _discoveryService.DiscoverMicroservicesAsync();
            }

            // Enrich microservices with cost information
            await EnrichMicroservicesWithCostDataAsync(microservices);

            return microservices;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving microservices from Azure");
            throw;
        }
    }

    public async Task<List<ResourceInfo>> GetSharedResourcesAsync()
    {
        try
        {
            // If shared resources are manually configured, use those
            if (_azureConfig.SharedResources?.Any() == true)
            {
                _logger.LogInformation("Using manually configured shared resources");
                return await GetManuallyConfiguredSharedResourcesAsync();
            }

            // Otherwise, use auto-discovery
            _logger.LogInformation("No manual configuration found, using auto-discovery");
            return await _discoveryService.DiscoverSharedResourcesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving shared resources from Azure");
            throw;
        }
    }

    private async Task<List<MicroserviceInfo>> GetManuallyConfiguredMicroservicesAsync()
    {
        var microservices = new List<MicroserviceInfo>();

        foreach (var microserviceConfig in _azureConfig.Microservices)
        {
            var microservice = new MicroserviceInfo
            {
                Name = microserviceConfig.Name,
                ResourceGroupName = microserviceConfig.ResourceGroupName,
                AppServices = new List<ResourceInfo>(),
                FunctionApps = new List<ResourceInfo>(),
                ServiceBus = new List<ResourceInfo>(),
                Databases = new List<ResourceInfo>(),
                StorageAccounts = new List<ResourceInfo>(),
                Other = new List<ResourceInfo>()
            };

            // Add App Service if configured
            if (!string.IsNullOrEmpty(microserviceConfig.AppServiceResourceId))
            {
                var appServiceInfo = await GetResourceInfoAsync(microserviceConfig.AppServiceResourceId);
                if (appServiceInfo != null)
                    microservice.AppServices.Add(appServiceInfo);
            }

            // Add Function Apps
            foreach (var functionAppId in microserviceConfig.FunctionAppResourceIds)
            {
                var functionAppInfo = await GetResourceInfoAsync(functionAppId);
                if (functionAppInfo != null)
                    microservice.FunctionApps.Add(functionAppInfo);
            }

            // Add Service Bus resources
            foreach (var serviceBusId in microserviceConfig.ServiceBusResourceIds)
            {
                var serviceBusInfo = await GetResourceInfoAsync(serviceBusId);
                if (serviceBusInfo != null)
                    microservice.ServiceBus.Add(serviceBusInfo);
            }

            // Add Database resources
            foreach (var databaseId in microserviceConfig.DatabaseResourceIds)
            {
                var databaseInfo = await GetResourceInfoAsync(databaseId);
                if (databaseInfo != null)
                    microservice.Databases.Add(databaseInfo);
            }

            microservices.Add(microservice);
        }

        return microservices;
    }

    private async Task<List<ResourceInfo>> GetManuallyConfiguredSharedResourcesAsync()
    {
        var sharedResources = new List<ResourceInfo>();

        foreach (var resourceId in _azureConfig.SharedResources)
        {
            var resourceInfo = await GetResourceInfoAsync(resourceId);
            if (resourceInfo != null)
                sharedResources.Add(resourceInfo);
        }

        return sharedResources;
    }

    private async Task<ResourceInfo?> GetResourceInfoAsync(string resourceId)
    {
        try
        {
            var resource = _armClient.GetGenericResource(new ResourceIdentifier(resourceId));
            var resourceData = await resource.GetAsync();

            return new ResourceInfo
            {
                Id = resourceData.Value.Id.ToString(),
                Name = resourceData.Value.Data.Name,
                Type = resourceData.Value.Data.ResourceType.ToString(),
                ResourceGroupName = ExtractResourceGroupFromId(resourceData.Value.Id.ToString()),
                Location = resourceData.Value.Data.Location.DisplayName ?? "Unknown",
                Tags = resourceData.Value.Data.Tags?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value) ?? new Dictionary<string, string>()
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not retrieve resource info for {ResourceId}", resourceId);
            return null;
        }
    }

    private string ExtractResourceGroupFromId(string resourceId)
    {
        var parts = resourceId.Split('/');
        var rgIndex = Array.IndexOf(parts, "resourceGroups");
        return rgIndex >= 0 && rgIndex + 1 < parts.Length ? parts[rgIndex + 1] : "Unknown";
    }

    public async Task<Dictionary<string, double>> GetResourceMetricsAsync(string resourceId, DateTime startTime, DateTime endTime)
    {
        try
        {
            var metrics = new Dictionary<string, double>();
            
            // Define common metrics to retrieve based on resource type
            var resourceType = ExtractResourceTypeFromId(resourceId);
            var metricsToQuery = GetMetricsForResourceType(resourceType);

            foreach (var metricName in metricsToQuery)
            {
                try
                {
                    var response = await _metricsClient.QueryResourceAsync(
                        resourceId,
                        new[] { metricName },
                        new MetricsQueryOptions
                        {
                            TimeRange = new QueryTimeRange(startTime, endTime),
                            Granularity = TimeSpan.FromHours(1)
                        });

                    double total = 0;
                    foreach (var metric in response.Value.Metrics)
                    {
                        foreach (var timeSeries in metric.TimeSeries)
                        {
                            foreach (var value in timeSeries.Values)
                            {
                                total += value.Average ?? value.Total ?? value.Maximum ?? 0;
                            }
                        }
                    }
                    metrics[metricName] = total;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not retrieve metric {MetricName} for resource {ResourceId}", metricName, resourceId);
                    metrics[metricName] = 0;
                }
            }

            return metrics;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving metrics for resource {ResourceId}", resourceId);
            return new Dictionary<string, double>();
        }
    }

    private string ExtractResourceTypeFromId(string resourceId)
    {
        // Extract resource type from resource ID
        // e.g., /subscriptions/.../providers/Microsoft.Web/sites/... -> Microsoft.Web/sites
        var parts = resourceId.Split('/');
        var providerIndex = Array.IndexOf(parts, "providers");
        if (providerIndex >= 0 && providerIndex + 2 < parts.Length)
        {
            return $"{parts[providerIndex + 1]}/{parts[providerIndex + 2]}";
        }
        return "Unknown";
    }

    private string[] GetMetricsForResourceType(string resourceType)
    {
        return resourceType.ToLower() switch
        {
            "microsoft.web/sites" => new[] { "CpuPercentage", "MemoryPercentage", "Http2xx", "Http4xx", "Http5xx", "RequestsInApplicationQueue" },
            "microsoft.servicebus/namespaces" => new[] { "IncomingMessages", "OutgoingMessages", "ActiveMessages" },
            "microsoft.sql/servers/databases" => new[] { "cpu_percent", "physical_data_read_percent", "log_write_percent", "dtu_consumption_percent" },
            "microsoft.documentdb/databaseaccounts" => new[] { "TotalRequestUnits", "ProvisionedThroughput", "DocumentCount" },
            "microsoft.storage/storageaccounts" => new[] { "Transactions", "UsedCapacity", "Ingress", "Egress" },
            _ => new[] { "CpuPercentage", "MemoryPercentage" }
        };
    }

    private async Task EnrichMicroservicesWithCostDataAsync(List<MicroserviceInfo> microservices)
    {
        try
        {
            _logger.LogInformation("Enriching microservices with cost data");

            var tasks = microservices.Select(async microservice =>
            {
                try
                {
                    // Calculate cost for this microservice
                    var costInfo = await _costService.GetMicroserviceCostAsync(microservice);
                    microservice.TotalCost = costInfo;

                    // Also enrich individual resources with cost data
                    await EnrichResourcesWithCostAsync(microservice.AppServices);
                    await EnrichResourcesWithCostAsync(microservice.FunctionApps);
                    await EnrichResourcesWithCostAsync(microservice.ServiceBus);
                    await EnrichResourcesWithCostAsync(microservice.Databases);
                    await EnrichResourcesWithCostAsync(microservice.StorageAccounts);
                    await EnrichResourcesWithCostAsync(microservice.Other);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, $"Failed to enrich microservice {microservice.Name} with cost data");
                }
            });

            await Task.WhenAll(tasks);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error enriching microservices with cost data");
        }
    }

    private async Task EnrichResourcesWithCostAsync(List<ResourceInfo> resources)
    {
        var tasks = resources.Select(async resource =>
        {
            try
            {
                var costInfo = await _costService.GetResourceCostAsync(resource.Id);
                resource.Cost = costInfo;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, $"Failed to get cost data for resource {resource.Name}");
            }
        });

        await Task.WhenAll(tasks);
    }
}
