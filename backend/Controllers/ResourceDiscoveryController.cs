using Microsoft.AspNetCore.Mvc;
using EnergyCalculator.Services;
using EnergyCalculator.Models;

namespace EnergyCalculator.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ResourceDiscoveryController : ControllerBase
{
    private readonly IAzureResourceDiscoveryService _discoveryService;
    private readonly ILogger<ResourceDiscoveryController> _logger;

    public ResourceDiscoveryController(
        IAzureResourceDiscoveryService discoveryService,
        ILogger<ResourceDiscoveryController> logger)
    {
        _discoveryService = discoveryService;
        _logger = logger;
    }

    /// <summary>
    /// Test Azure connection
    /// </summary>
    [HttpGet("test-connection")]
    public async Task<IActionResult> TestConnection()
    {
        try
        {
            var isConnected = await _discoveryService.TestConnectionAsync();
            return Ok(new { connected = isConnected });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing Azure connection");
            return StatusCode(500, new { error = "Failed to test Azure connection", details = ex.Message });
        }
    }

    /// <summary>
    /// Discover all resources in the Azure subscription
    /// </summary>
    [HttpGet("resources")]
    public async Task<IActionResult> GetAllResources()
    {
        try
        {
            var resources = await _discoveryService.DiscoverAllResourcesAsync();
            return Ok(new 
            { 
                totalCount = resources.Count,
                resources = resources.GroupBy(r => r.Type)
                    .Select(g => new 
                    {
                        resourceType = g.Key,
                        count = g.Count(),
                        resources = g.ToList()
                    })
                    .OrderByDescending(x => x.count)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error discovering all resources");
            return StatusCode(500, new { error = "Failed to discover resources", details = ex.Message });
        }
    }

    /// <summary>
    /// Discover all resource groups and their resources
    /// </summary>
    [HttpGet("resource-groups")]
    public async Task<IActionResult> GetResourceGroups()
    {
        try
        {
            var resourceGroups = await _discoveryService.DiscoverAllResourceGroupsAsync();
            return Ok(new 
            { 
                totalCount = resourceGroups.Count,
                totalResources = resourceGroups.Sum(rg => rg.Resources.Count),
                resourceGroups = resourceGroups.OrderByDescending(rg => rg.Resources.Count)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error discovering resource groups");
            return StatusCode(500, new { error = "Failed to discover resource groups", details = ex.Message });
        }
    }

    /// <summary>
    /// Auto-discover microservices based on resource grouping and naming conventions
    /// </summary>
    [HttpGet("microservices")]
    public async Task<IActionResult> DiscoverMicroservices()
    {
        try
        {
            var microservices = await _discoveryService.DiscoverMicroservicesAsync();
            return Ok(new 
            { 
                totalCount = microservices.Count,
                totalResources = microservices.Sum(ms => ms.TotalResourceCount),
                microservices = microservices.OrderByDescending(ms => ms.TotalResourceCount)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error discovering microservices");
            return StatusCode(500, new { error = "Failed to discover microservices", details = ex.Message });
        }
    }

    /// <summary>
    /// Discover shared infrastructure resources
    /// </summary>
    [HttpGet("shared-resources")]
    public async Task<IActionResult> DiscoverSharedResources()
    {
        try
        {
            var sharedResources = await _discoveryService.DiscoverSharedResourcesAsync();
            return Ok(new 
            { 
                totalCount = sharedResources.Count,
                resources = sharedResources.GroupBy(r => r.Type)
                    .Select(g => new 
                    {
                        resourceType = g.Key,
                        count = g.Count(),
                        resources = g.ToList()
                    })
                    .OrderByDescending(x => x.count)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error discovering shared resources");
            return StatusCode(500, new { error = "Failed to discover shared resources", details = ex.Message });
        }
    }

    /// <summary>
    /// Get a summary of the subscription's resources organized by category
    /// </summary>
    [HttpGet("summary")]
    public async Task<IActionResult> GetDiscoverySummary()
    {
        try
        {
            var microservices = await _discoveryService.DiscoverMicroservicesAsync();
            var sharedResources = await _discoveryService.DiscoverSharedResourcesAsync();
            var allResourceGroups = await _discoveryService.DiscoverAllResourceGroupsAsync();

            var summary = new
            {
                subscription = new
                {
                    totalResourceGroups = allResourceGroups.Count,
                    totalResources = allResourceGroups.Sum(rg => rg.Resources.Count),
                    locations = allResourceGroups.Select(rg => rg.Location).Distinct().OrderBy(l => l).ToList()
                },
                microservices = new
                {
                    count = microservices.Count,
                    totalResources = microservices.Sum(ms => ms.TotalResourceCount),
                    services = microservices.Select(ms => new
                    {
                        name = ms.Name,
                        resourceGroup = ms.ResourceGroupName,
                        resourceCount = ms.TotalResourceCount,
                        breakdown = new
                        {
                            appServices = ms.AppServices.Count,
                            functionApps = ms.FunctionApps.Count,
                            databases = ms.Databases.Count,
                            serviceBus = ms.ServiceBus.Count,
                            storage = ms.StorageAccounts.Count,
                            other = ms.Other.Count
                        }
                    }).OrderByDescending(s => s.resourceCount)
                },
                sharedInfrastructure = new
                {
                    count = sharedResources.Count,
                    byType = sharedResources.GroupBy(r => r.Type)
                        .Select(g => new { type = g.Key, count = g.Count() })
                        .OrderByDescending(x => x.count)
                },
                resourceTypes = allResourceGroups
                    .SelectMany(rg => rg.Resources)
                    .GroupBy(r => r.Type)
                    .Select(g => new { type = g.Key, count = g.Count() })
                    .OrderByDescending(x => x.count)
                    .Take(10)
            };

            return Ok(summary);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating discovery summary");
            return StatusCode(500, new { error = "Failed to generate discovery summary", details = ex.Message });
        }
    }
}
