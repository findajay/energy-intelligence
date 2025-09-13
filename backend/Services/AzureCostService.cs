using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using EnergyCalculator.Models;
using Microsoft.Extensions.Options;
using System.Text.Json;
using System.Text;

namespace EnergyCalculator.Services;

public interface IAzureCostService
{
    Task<CostInfo?> GetResourceCostAsync(string resourceId);
    Task<CostInfo?> GetResourceGroupCostAsync(string resourceGroupName);
    Task<CostInfo?> GetMicroserviceCostAsync(MicroserviceInfo microservice);
    Task<Dictionary<string, decimal>> GetSubscriptionCostSummaryAsync();
}

public class AzureCostService : IAzureCostService
{
    private readonly ArmClient _armClient;
    private readonly AzureConfiguration _azureConfig;
    private readonly ILogger<AzureCostService> _logger;
    private readonly bool _useRealCostData;
    private readonly TokenCredential _credential;
    private readonly HttpClient _httpClient;

    public AzureCostService(
        IOptions<AzureConfiguration> azureConfig,
        ILogger<AzureCostService> logger,
        HttpClient httpClient)
    {
        _azureConfig = azureConfig.Value;
        _logger = logger;
        _httpClient = httpClient;

        // Create credential based on configuration
        _credential = _azureConfig.Authentication.Type switch
        {
            "ServicePrincipal" => new ClientSecretCredential(
                _azureConfig.TenantId,
                _azureConfig.Authentication.ServicePrincipal.ClientId,
                _azureConfig.Authentication.ServicePrincipal.ClientSecret),
            _ => new DefaultAzureCredential()
        };

        _armClient = new ArmClient(_credential);
        
        // Enable real cost data based on configuration and authentication
        _useRealCostData = _azureConfig.CostManagement.Enabled && 
                          _azureConfig.CostManagement.UseRealData && 
                          _azureConfig.Authentication.Type == "ServicePrincipal";
        
        if (_useRealCostData)
        {
            _logger.LogInformation("✅ Real Azure Cost Management API enabled - will fetch actual cost data");
        }
        else
        {
            _logger.LogWarning("⚠️  Using enhanced mock cost data - set CostManagement.UseRealData=true to enable real costs");
        }
    }

    public async Task<CostInfo?> GetResourceCostAsync(string resourceId)
    {
        try
        {
            _logger.LogInformation($"Fetching cost data for resource: {resourceId}");

            if (!_useRealCostData)
            {
                _logger.LogDebug("Using mock cost data for resource {ResourceId}", resourceId);
                return GenerateMockCostData(resourceId);
            }

            // Try to get real cost data, fallback to mock on error
            try
            {
                return await GetRealResourceCostAsync(resourceId);
            }
            catch (Exception realCostEx)
            {
                _logger.LogWarning(realCostEx, "Failed to get real cost data for resource {ResourceId}, using mock data", resourceId);
                return GenerateMockCostData(resourceId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error fetching cost data for resource {resourceId}");
            return null;
        }
    }

    public Task<CostInfo?> GetResourceGroupCostAsync(string resourceGroupName)
    {
        try
        {
            _logger.LogInformation($"Fetching cost data for resource group: {resourceGroupName}");

            // For now, we'll return mock data
            return Task.FromResult<CostInfo?>(GenerateMockResourceGroupCostData(resourceGroupName));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error fetching cost data for resource group {resourceGroupName}");
            return Task.FromResult<CostInfo?>(null);
        }
    }

    public async Task<CostInfo?> GetMicroserviceCostAsync(MicroserviceInfo microservice)
    {
        try
        {
            _logger.LogInformation($"Calculating total cost for microservice: {microservice.Name}");

            var totalDailyCost = 0m;
            var totalMonthlyCost = 0m;
            var breakdown = new List<CostBreakdown>();

            // Calculate costs for all resources in the microservice
            foreach (var appService in microservice.AppServices)
            {
                var cost = await GetResourceCostAsync(appService.Id);
                if (cost != null)
                {
                    totalDailyCost += cost.DailyCost;
                    totalMonthlyCost += cost.MonthlyCost;
                    breakdown.Add(new CostBreakdown
                    {
                        Category = "App Service",
                        Amount = cost.MonthlyCost,
                        MeterName = appService.Name
                    });
                }
            }

            foreach (var functionApp in microservice.FunctionApps)
            {
                var cost = await GetResourceCostAsync(functionApp.Id);
                if (cost != null)
                {
                    totalDailyCost += cost.DailyCost;
                    totalMonthlyCost += cost.MonthlyCost;
                    breakdown.Add(new CostBreakdown
                    {
                        Category = "Function App",
                        Amount = cost.MonthlyCost,
                        MeterName = functionApp.Name
                    });
                }
            }

            // Add costs for other resource types
            var allOtherResources = microservice.ServiceBus
                .Concat(microservice.Databases)
                .Concat(microservice.StorageAccounts)
                .Concat(microservice.Other);

            foreach (var resource in allOtherResources)
            {
                var cost = await GetResourceCostAsync(resource.Id);
                if (cost != null)
                {
                    totalDailyCost += cost.DailyCost;
                    totalMonthlyCost += cost.MonthlyCost;
                    breakdown.Add(new CostBreakdown
                    {
                        Category = GetResourceCategory(resource.Type),
                        Amount = cost.MonthlyCost,
                        MeterName = resource.Name
                    });
                }
            }

            return new CostInfo
            {
                DailyCost = totalDailyCost,
                MonthlyCost = totalMonthlyCost,
                YearlyEstimate = totalMonthlyCost * 12,
                Currency = "USD",
                LastUpdated = DateTime.UtcNow,
                Breakdown = breakdown
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error calculating cost for microservice {microservice.Name}");
            return null;
        }
    }

    public async Task<Dictionary<string, decimal>> GetSubscriptionCostSummaryAsync()
    {
        try
        {
            _logger.LogInformation("Fetching subscription cost summary");

            if (!_useRealCostData)
            {
                _logger.LogDebug("Using mock cost data for subscription summary");
                return GetMockSubscriptionCostSummary();
            }

            try
            {
                return await GetRealSubscriptionCostSummaryAsync();
            }
            catch (Exception realCostEx)
            {
                _logger.LogWarning(realCostEx, "Failed to get real subscription cost summary, using mock data");
                return GetMockSubscriptionCostSummary();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching subscription cost summary");
            return new Dictionary<string, decimal>();
        }
    }

    private async Task<Dictionary<string, decimal>> GetRealSubscriptionCostSummaryAsync()
    {
        try
        {
            _logger.LogDebug("🔍 Fetching real subscription cost summary from Azure Cost Management API");

            var subscriptionId = _azureConfig.SubscriptionId;
            
            // Get cost data for the last 30 days grouped by service category
            var endDate = DateTime.UtcNow.Date;
            var startDate = endDate.AddDays(-30);
            
            // Create the cost management query payload for subscription summary
            var queryPayload = new
            {
                type = "ActualCost",
                timeframe = "Custom",
                timePeriod = new
                {
                    from = startDate.ToString("yyyy-MM-dd"),
                    to = endDate.ToString("yyyy-MM-dd")
                },
                dataset = new
                {
                    granularity = "Monthly",
                    aggregation = new Dictionary<string, object>
                    {
                        ["PreTaxCost"] = new { name = "PreTaxCost", function = "Sum" }
                    },
                    grouping = new[]
                    {
                        new { type = "Dimension", name = "ServiceName" },
                        new { type = "Dimension", name = "MeterCategory" }
                    }
                }
            };

            // Get access token
            var tokenContext = new TokenRequestContext(new[] { "https://management.azure.com/.default" });
            var accessToken = await _credential.GetTokenAsync(tokenContext, CancellationToken.None);

            // Prepare HTTP request
            var apiUrl = $"https://management.azure.com/subscriptions/{subscriptionId}/providers/Microsoft.CostManagement/query?api-version=2023-03-01";
            
            var json = JsonSerializer.Serialize(queryPayload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            
            _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken.Token);

            // Make the API call
            var response = await _httpClient.PostAsync(apiUrl, content);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("❌ Subscription Cost Management API failed: {StatusCode} - {Error}", response.StatusCode, errorContent);
                throw new Exception($"Cost Management API returned {response.StatusCode}: {errorContent}");
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            var costData = JsonSerializer.Deserialize<JsonElement>(responseContent);

            var costSummary = new Dictionary<string, decimal>();

            // Parse the response
            if (costData.TryGetProperty("properties", out var properties) &&
                properties.TryGetProperty("rows", out var rows))
            {
                foreach (var row in rows.EnumerateArray())
                {
                    var rowData = row.EnumerateArray().ToArray();
                    // Row structure: [Cost, ServiceName, MeterCategory]
                    if (rowData.Length >= 3 && 
                        rowData[0].TryGetDecimal(out var cost) && cost > 0)
                    {
                        var serviceName = rowData[1].GetString() ?? "Other";
                        var category = MapServiceToCategory(serviceName, rowData[2].GetString());
                        
                        if (costSummary.ContainsKey(category))
                            costSummary[category] += cost;
                        else
                            costSummary[category] = cost;
                    }
                }
            }

            _logger.LogInformation("💰 Real subscription cost summary retrieved: {TotalCategories} categories, ${TotalCost}", 
                costSummary.Count, costSummary.Values.Sum());

            // If no data found, fallback to mock
            return costSummary.Any() ? 
                costSummary.ToDictionary(kvp => kvp.Key, kvp => Math.Round(kvp.Value, 2)) : 
                GetMockSubscriptionCostSummary();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error fetching real subscription cost summary");
            throw; // Will be handled by caller with fallback to mock
        }
    }

    private string MapServiceToCategory(string serviceName, string? meterCategory)
    {
        // Map Azure services to broader categories for better grouping
        var service = serviceName?.ToLower() ?? "";
        var meter = meterCategory?.ToLower() ?? "";
        
        return service switch
        {
            var s when s.Contains("compute") || s.Contains("virtual machines") || s.Contains("app service") => "Compute",
            var s when s.Contains("storage") || s.Contains("blob") || s.Contains("file") => "Storage",
            var s when s.Contains("network") || s.Contains("bandwidth") || s.Contains("cdn") => "Networking",
            var s when s.Contains("sql") || s.Contains("database") || s.Contains("cosmos") => "Databases",
            var s when s.Contains("analytics") || s.Contains("synapse") || s.Contains("databricks") => "Analytics",
            var s when s.Contains("security") || s.Contains("key vault") || s.Contains("sentinel") => "Security",
            var s when s.Contains("service bus") || s.Contains("event") || s.Contains("logic") => "Integration",
            _ => meter switch
            {
                var m when m.Contains("compute") => "Compute",
                var m when m.Contains("storage") => "Storage",
                var m when m.Contains("network") => "Networking",
                var m when m.Contains("data") => "Databases",
                _ => "Other"
            }
        };
    }

    private Dictionary<string, decimal> GetMockSubscriptionCostSummary()
    {
        return new Dictionary<string, decimal>
        {
            ["Compute"] = 1250.50m,
            ["Storage"] = 175.25m,
            ["Networking"] = 95.75m,
            ["Databases"] = 340.00m,
            ["Other"] = 125.30m
        };
    }

    private async Task<CostInfo?> GetRealResourceCostAsync(string resourceId)
    {
        try
        {
            _logger.LogDebug("🔍 Fetching real cost data from Azure Cost Management API for resource: {ResourceId}", resourceId);

            // Get actual cost data using Azure Cost Management REST API
            var subscriptionId = _azureConfig.SubscriptionId;
            
            // Get cost data for the last 30 days
            var endDate = DateTime.UtcNow.Date;
            var startDate = endDate.AddDays(-30);
            
            _logger.LogDebug("📊 Querying cost management REST API for period: {StartDate} to {EndDate}", startDate, endDate);
            
            // Create the cost management query payload
            var queryPayload = new
            {
                type = "ActualCost",
                timeframe = "Custom",
                timePeriod = new
                {
                    from = startDate.ToString("yyyy-MM-dd"),
                    to = endDate.ToString("yyyy-MM-dd")
                },
                dataset = new
                {
                    granularity = "Daily",
                    aggregation = new Dictionary<string, object>
                    {
                        ["PreTaxCost"] = new { name = "PreTaxCost", function = "Sum" }
                    },
                    grouping = new[]
                    {
                        new { type = "Dimension", name = "ResourceId" },
                        new { type = "Dimension", name = "MeterCategory" },
                        new { type = "Dimension", name = "ResourceType" }
                    },
                    filter = new
                    {
                        dimensions = new
                        {
                            name = "ResourceId",
                            @operator = "In",
                            values = new[] { resourceId }
                        }
                    }
                }
            };

            // Get access token
            var tokenContext = new TokenRequestContext(new[] { "https://management.azure.com/.default" });
            var accessToken = await _credential.GetTokenAsync(tokenContext, CancellationToken.None);

            // Prepare HTTP request
            var apiUrl = $"https://management.azure.com/subscriptions/{subscriptionId}/providers/Microsoft.CostManagement/query?api-version=2023-03-01";
            
            var json = JsonSerializer.Serialize(queryPayload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            
            _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken.Token);

            // Make the API call
            var response = await _httpClient.PostAsync(apiUrl, content);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("❌ Cost Management API failed: {StatusCode} - {Error}", response.StatusCode, errorContent);
                throw new Exception($"Cost Management API returned {response.StatusCode}: {errorContent}");
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            var costData = JsonSerializer.Deserialize<JsonElement>(responseContent);

            var totalCost = 0m;
            var breakdown = new Dictionary<string, decimal>();
            var recordCount = 0;

            // Parse the response
            if (costData.TryGetProperty("properties", out var properties) &&
                properties.TryGetProperty("rows", out var rows))
            {
                foreach (var row in rows.EnumerateArray())
                {
                    recordCount++;
                    
                    var rowData = row.EnumerateArray().ToArray();
                    // Row structure: [Cost, ResourceId, MeterCategory, ResourceType, Date]
                    if (rowData.Length >= 3 && 
                        rowData[0].TryGetDecimal(out var cost) && cost > 0)
                    {
                        totalCost += cost;
                        
                        // Group by meter category for breakdown
                        var category = rowData.Length > 2 ? rowData[2].GetString() ?? "Other" : "Other";
                        if (breakdown.ContainsKey(category))
                            breakdown[category] += cost;
                        else
                            breakdown[category] = cost;
                    }
                }
            }

            _logger.LogInformation("💰 Real cost data retrieved: ${Cost} over {Days} days ({Records} cost records)", 
                totalCost, (endDate - startDate).Days, recordCount);

            if (totalCost == 0 && recordCount == 0)
            {
                _logger.LogWarning("⚠️  No cost records found for resource {ResourceId}, might be too new or inactive", resourceId);
                return null; // Will fallback to mock data
            }

            // Calculate daily and yearly estimates
            var daysInPeriod = Math.Max(1, (endDate - startDate).Days);
            var dailyCost = totalCost / daysInPeriod;
            var monthlyCost = dailyCost * 30; // Convert to monthly estimate
            var yearlyCost = dailyCost * 365;

            var costBreakdown = breakdown.Any() ? 
                breakdown.Select(kvp => new CostBreakdown
                {
                    Category = kvp.Key,
                    Amount = Math.Round(kvp.Value, 2),
                    MeterName = $"{kvp.Key} Usage"
                }).ToList() :
                new List<CostBreakdown>
                {
                    new() { Category = "Total Cost", Amount = Math.Round(totalCost, 2), MeterName = "Azure Resource Cost" }
                };

            return new CostInfo
            {
                DailyCost = Math.Round(dailyCost, 2),
                MonthlyCost = Math.Round(monthlyCost, 2),
                YearlyEstimate = Math.Round(yearlyCost, 2),
                Currency = "USD",
                LastUpdated = DateTime.UtcNow,
                Breakdown = costBreakdown
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error fetching real cost data for resource {ResourceId}", resourceId);
            throw; // Let calling method handle the fallback
        }
    }

    private async Task<CostInfo> GenerateEnhancedMockCostDataAsync(string resourceId)
    {
        // Simulate API delay
        await Task.Delay(50);
        
        // Extract resource type and location from resource ID for more realistic costs
        var resourceType = ExtractResourceTypeFromId(resourceId);
        var location = ExtractLocationFromId(resourceId) ?? "East US";
        
        // Generate more realistic costs based on actual Azure pricing patterns
        var baseCost = GetBaseCostForResourceType(resourceType);
        var locationMultiplier = GetLocationCostMultiplier(location);
        
        var random = new Random(resourceId.GetHashCode());
        var utilizationFactor = 0.6 + (random.NextDouble() * 0.4); // 60-100% utilization
        var dailyCost = (decimal)(baseCost * locationMultiplier * utilizationFactor);
        
        return new CostInfo
        {
            DailyCost = Math.Round(dailyCost, 2),
            MonthlyCost = Math.Round(dailyCost * 30, 2),
            YearlyEstimate = Math.Round(dailyCost * 365, 2),
            Currency = "USD",
            LastUpdated = DateTime.UtcNow,
            Breakdown = GenerateCostBreakdownForResourceType(resourceType, dailyCost * 30)
        };
    }

    private string? ExtractResourceTypeFromId(string resourceId)
    {
        try
        {
            var parts = resourceId.Split('/');
            var providerIndex = Array.IndexOf(parts, "providers");
            if (providerIndex >= 0 && providerIndex + 2 < parts.Length)
            {
                return $"{parts[providerIndex + 1]}/{parts[providerIndex + 2]}";
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not extract resource type from ID: {ResourceId}", resourceId);
        }
        return "Unknown";
    }

    private string? ExtractLocationFromId(string resourceId)
    {
        // In a real implementation, you'd make an ARM call to get the location
        // For now, return a default location
        return "East US";
    }

    private double GetBaseCostForResourceType(string? resourceType)
    {
        return resourceType?.ToLower() switch
        {
            "microsoft.web/sites" => 25.0, // App Service
            "microsoft.web/serverfarms" => 15.0, // App Service Plan
            "microsoft.sql/servers/databases" => 45.0, // SQL Database
            "microsoft.documentdb/databaseaccounts" => 35.0, // Cosmos DB
            "microsoft.storage/storageaccounts" => 5.0, // Storage Account
            "microsoft.servicebus/namespaces" => 10.0, // Service Bus
            "microsoft.keyvault/vaults" => 1.0, // Key Vault
            "microsoft.insights/components" => 2.0, // Application Insights
            "microsoft.cache/redis" => 30.0, // Redis Cache
            "microsoft.network/applicationgateways" => 40.0, // Application Gateway
            _ => 8.0 // Default for unknown resources
        };
    }

    private double GetLocationCostMultiplier(string location)
    {
        return location.ToLower() switch
        {
            "east us" => 1.0,
            "west us" => 1.05,
            "west europe" => 1.1,
            "north europe" => 1.08,
            "southeast asia" => 1.15,
            "japan east" => 1.2,
            _ => 1.0
        };
    }

    private List<CostBreakdown> GenerateCostBreakdownForResourceType(string? resourceType, decimal totalMonthlyCost)
    {
        return resourceType?.ToLower() switch
        {
            "microsoft.web/sites" => new List<CostBreakdown>
            {
                new() { Category = "Compute", Amount = totalMonthlyCost * 0.7m, MeterName = "App Service Compute Hours" },
                new() { Category = "Storage", Amount = totalMonthlyCost * 0.2m, MeterName = "App Service Storage" },
                new() { Category = "Bandwidth", Amount = totalMonthlyCost * 0.1m, MeterName = "Data Transfer" }
            },
            "microsoft.sql/servers/databases" => new List<CostBreakdown>
            {
                new() { Category = "Compute", Amount = totalMonthlyCost * 0.6m, MeterName = "SQL Database DTUs" },
                new() { Category = "Storage", Amount = totalMonthlyCost * 0.3m, MeterName = "SQL Database Storage" },
                new() { Category = "Backup", Amount = totalMonthlyCost * 0.1m, MeterName = "Backup Storage" }
            },
            "microsoft.storage/storageaccounts" => new List<CostBreakdown>
            {
                new() { Category = "Storage", Amount = totalMonthlyCost * 0.8m, MeterName = "Blob Storage" },
                new() { Category = "Transactions", Amount = totalMonthlyCost * 0.15m, MeterName = "Storage Transactions" },
                new() { Category = "Bandwidth", Amount = totalMonthlyCost * 0.05m, MeterName = "Data Transfer" }
            },
            _ => new List<CostBreakdown>
            {
                new() { Category = "Compute", Amount = totalMonthlyCost * 0.6m, MeterName = "Compute Usage" },
                new() { Category = "Storage", Amount = totalMonthlyCost * 0.3m, MeterName = "Storage Usage" },
                new() { Category = "Network", Amount = totalMonthlyCost * 0.1m, MeterName = "Network Usage" }
            }
        };
    }

    private CostInfo GenerateMockCostData(string resourceId)
    {
        // Generate realistic mock cost data based on resource type
        var random = new Random(resourceId.GetHashCode());
        var baseDaily = random.Next(5, 50) + (decimal)random.NextDouble();
        
        return new CostInfo
        {
            DailyCost = baseDaily,
            MonthlyCost = baseDaily * 30,
            YearlyEstimate = baseDaily * 365,
            Currency = "USD",
            LastUpdated = DateTime.UtcNow,
            Breakdown = new List<CostBreakdown>
            {
                new() { Category = "Compute", Amount = baseDaily * 0.6m * 30, MeterName = "Standard Compute Hours" },
                new() { Category = "Storage", Amount = baseDaily * 0.3m * 30, MeterName = "Standard Storage" },
                new() { Category = "Network", Amount = baseDaily * 0.1m * 30, MeterName = "Data Transfer" }
            }
        };
    }

    private CostInfo GenerateMockResourceGroupCostData(string resourceGroupName)
    {
        var random = new Random(resourceGroupName.GetHashCode());
        var baseDaily = random.Next(100, 500) + (decimal)random.NextDouble();
        
        return new CostInfo
        {
            DailyCost = baseDaily,
            MonthlyCost = baseDaily * 30,
            YearlyEstimate = baseDaily * 365,
            Currency = "USD",
            LastUpdated = DateTime.UtcNow,
            Breakdown = new List<CostBreakdown>
            {
                new() { Category = "App Services", Amount = baseDaily * 0.4m * 30, MeterName = "App Service Plans" },
                new() { Category = "Databases", Amount = baseDaily * 0.3m * 30, MeterName = "SQL Database" },
                new() { Category = "Storage", Amount = baseDaily * 0.2m * 30, MeterName = "Storage Accounts" },
                new() { Category = "Other", Amount = baseDaily * 0.1m * 30, MeterName = "Miscellaneous" }
            }
        };
    }

    private string GetResourceCategory(string resourceType)
    {
        return resourceType.ToLower() switch
        {
            var type when type.Contains("webapp") => "App Service",
            var type when type.Contains("function") => "Function App",
            var type when type.Contains("sql") => "Database",
            var type when type.Contains("storage") => "Storage",
            var type when type.Contains("servicebus") => "Service Bus",
            var type when type.Contains("network") => "Networking",
            _ => "Other"
        };
    }
}
