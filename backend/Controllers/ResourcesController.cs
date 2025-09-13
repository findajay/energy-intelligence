using Microsoft.AspNetCore.Mvc;
using EnergyCalculator.Services;
using EnergyCalculator.Models;

namespace EnergyCalculator.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ResourcesController : ControllerBase
{
    private readonly IAzureResourceService _azureResourceService;
    private readonly IAzureCostService _azureCostService;
    private readonly ILogger<ResourcesController> _logger;
    private readonly IConfiguration _configuration;

    public ResourcesController(
        IAzureResourceService azureResourceService,
        IAzureCostService azureCostService,
        ILogger<ResourcesController> logger,
        IConfiguration configuration)
    {
        _azureResourceService = azureResourceService;
        _azureCostService = azureCostService;
        _logger = logger;
        _configuration = configuration;
    }

    [HttpGet("test-connection")]
    public async Task<IActionResult> TestConnection()
    {
        try
        {
            var isConnected = await _azureResourceService.TestConnectionAsync();
            return Ok(new { connected = isConnected, timestamp = DateTime.UtcNow });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing Azure connection");
            return StatusCode(500, new { error = ex.Message, connected = false });
        }
    }

    [HttpGet("microservices")]
    public async Task<IActionResult> GetMicroservices()
    {
        try
        {
            var microservices = await _azureResourceService.GetMicroservicesAsync();
            return Ok(microservices);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving microservices");
            
            // Fallback to mock data if Azure connection fails
            var mockMicroservices = GetMockMicroservices();
            return Ok(new { 
                data = mockMicroservices,
                warning = "Using mock data - Azure connection failed",
                error = ex.Message 
            });
        }
    }

    [HttpGet("shared")]
    public async Task<IActionResult> GetSharedResources()
    {
        try
        {
            var sharedResources = await _azureResourceService.GetSharedResourcesAsync();
            return Ok(sharedResources);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving shared resources");
            
            // Fallback to mock data if Azure connection fails
            var mockSharedResources = GetMockSharedResources();
            return Ok(new { 
                data = mockSharedResources,
                warning = "Using mock data - Azure connection failed",
                error = ex.Message 
            });
        }
    }

    private List<MicroserviceInfo> GetMockMicroservices()
    {
        var subscriptionId = _configuration["Azure:SubscriptionId"] ?? "YOUR_SUBSCRIPTION_ID";
        
        return new List<MicroserviceInfo>
        {
            new MicroserviceInfo
            {
                Name = "PaymentService",
                AppServices = new List<ResourceInfo>
                {
                    new ResourceInfo { Id = $"/subscriptions/{subscriptionId}/resourceGroups/payment-rg/providers/Microsoft.Web/sites/payment-api", Name = "payment-api", Type = "Microsoft.Web/sites" }
                },
                FunctionApps = new List<ResourceInfo>
                {
                    new ResourceInfo { Id = $"/subscriptions/{subscriptionId}/resourceGroups/payment-rg/providers/Microsoft.Web/sites/payment-functions", Name = "payment-functions", Type = "Microsoft.Web/sites" }
                },
                ServiceBus = new List<ResourceInfo>
                {
                    new ResourceInfo { Id = $"/subscriptions/{subscriptionId}/resourceGroups/payment-rg/providers/Microsoft.ServiceBus/namespaces/payment-bus", Name = "payment-bus", Type = "Microsoft.ServiceBus/namespaces" }
                },
                Databases = new List<ResourceInfo>
                {
                    new ResourceInfo { Id = $"/subscriptions/{subscriptionId}/resourceGroups/payment-rg/providers/Microsoft.Sql/servers/payment-db", Name = "payment-db", Type = "Microsoft.Sql/servers" }
                }
            },
            new MicroserviceInfo
            {
                Name = "SessionsService",
                AppServices = new List<ResourceInfo>
                {
                    new ResourceInfo { Id = $"/subscriptions/{subscriptionId}/resourceGroups/sessions-rg/providers/Microsoft.Web/sites/sessions-api", Name = "sessions-api", Type = "Microsoft.Web/sites" }
                },
                FunctionApps = new List<ResourceInfo>
                {
                    new ResourceInfo { Id = $"/subscriptions/{subscriptionId}/resourceGroups/sessions-rg/providers/Microsoft.Web/sites/sessions-functions", Name = "sessions-functions", Type = "Microsoft.Web/sites" }
                },
                ServiceBus = new List<ResourceInfo>
                {
                    new ResourceInfo { Id = $"/subscriptions/{subscriptionId}/resourceGroups/sessions-rg/providers/Microsoft.ServiceBus/namespaces/sessions-bus", Name = "sessions-bus", Type = "Microsoft.ServiceBus/namespaces" }
                },
                Databases = new List<ResourceInfo>
                {
                    new ResourceInfo { Id = $"/subscriptions/{subscriptionId}/resourceGroups/sessions-rg/providers/Microsoft.DocumentDB/databaseAccounts/sessions-cosmos", Name = "sessions-cosmos", Type = "Microsoft.DocumentDB/databaseAccounts" }
                }
            }
        };
    }

    private List<ResourceInfo> GetMockSharedResources()
    {
        var subscriptionId = _configuration["Azure:SubscriptionId"] ?? "YOUR_SUBSCRIPTION_ID";
        
        return new List<ResourceInfo>
        {
            new ResourceInfo { Id = $"/subscriptions/{subscriptionId}/resourceGroups/shared-rg/providers/Microsoft.ServiceBus/namespaces/shared-bus", Name = "shared-bus", Type = "Microsoft.ServiceBus/namespaces" },
            new ResourceInfo { Id = $"/subscriptions/{subscriptionId}/resourceGroups/shared-rg/providers/Microsoft.KeyVault/vaults/shared-keyvault", Name = "shared-keyvault", Type = "Microsoft.KeyVault/vaults" },
            new ResourceInfo { Id = $"/subscriptions/{subscriptionId}/resourceGroups/shared-rg/providers/Microsoft.Storage/storageAccounts/sharedstorage", Name = "sharedstorage", Type = "Microsoft.Storage/storageAccounts" }
        };
    }

    [HttpGet("costs/summary")]
    public async Task<IActionResult> GetCostSummary()
    {
        try
        {
            var costSummary = await _azureCostService.GetSubscriptionCostSummaryAsync();
            return Ok(new { 
                summary = costSummary,
                totalMonthly = costSummary.Values.Sum(),
                currency = "USD",
                lastUpdated = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving cost summary");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpGet("costs/resource-group/{resourceGroupName}")]
    public async Task<IActionResult> GetResourceGroupCost(string resourceGroupName)
    {
        try
        {
            var cost = await _azureCostService.GetResourceGroupCostAsync(resourceGroupName);
            if (cost == null)
            {
                return NotFound(new { error = $"Cost data not found for resource group: {resourceGroupName}" });
            }

            return Ok(cost);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving resource group cost for {ResourceGroup}", resourceGroupName);
            return StatusCode(500, new { error = ex.Message });
        }
    }
}
