using Microsoft.AspNetCore.Mvc;
using Azure.Identity;
using Azure.ResourceManager;

namespace EnergyCalculator.Controllers;

[ApiController]
[Route("[controller]")]
public class HealthController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<HealthController> _logger;

    public HealthController(
        IConfiguration configuration,
        ILogger<HealthController> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Check()
    {
        try
        {
            // Check Azure connectivity using service principal from configuration
            var clientId = _configuration["AZURE_CLIENT_ID"];
            var clientSecret = _configuration["AZURE_CLIENT_SECRET"];
            var tenantId = _configuration["AZURE_TENANT_ID"];
            
            Azure.Core.TokenCredential credential;
            if (!string.IsNullOrEmpty(clientId) && !string.IsNullOrEmpty(clientSecret) && !string.IsNullOrEmpty(tenantId))
            {
                credential = new ClientSecretCredential(tenantId, clientId, clientSecret);
            }
            else
            {
                credential = new DefaultAzureCredential();
            }
                
            var armClient = new ArmClient(credential);
            var subscription = await armClient.GetDefaultSubscriptionAsync();

            return Ok(new
            {
                status = "healthy",
                timestamp = DateTime.UtcNow,
                azureConnectivity = true,
                subscriptionId = _configuration["Azure:SubscriptionId"]
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Health check failed");
            return StatusCode(500, new
            {
                status = "unhealthy",
                timestamp = DateTime.UtcNow,
                error = ex.Message
            });
        }
    }
}
