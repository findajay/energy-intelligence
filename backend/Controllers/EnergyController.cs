using Microsoft.AspNetCore.Mvc;
using EnergyCalculator.Services;
using EnergyCalculator.Models;
using EnergyCore;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.Resources;
using Microsoft.Extensions.Options;
using Azure.Core;
using System.Text.Json;
using System.Collections.Concurrent;

namespace EnergyCalculator.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EnergyController : ControllerBase
{
    private readonly IEnergyCalculatorService _calculatorService;
    private readonly IEnergyStorageService _storageService;
    private readonly ILogger<EnergyController> _logger;
    private readonly IConfiguration _configuration;
    private readonly ArmClient _armClient;
    private readonly AzureConfiguration _azureConfig;
    
    // Performance caching to avoid repeated Azure API calls
    private static readonly ConcurrentDictionary<string, string> _skuCache = new();
    private static readonly ConcurrentDictionary<string, DateTime?> _creationDateCache = new();
    private static readonly object _cacheLock = new();

    public EnergyController(
        IEnergyCalculatorService calculatorService,
        IEnergyStorageService storageService,
        ILogger<EnergyController> logger,
        IConfiguration configuration,
        IOptions<AzureConfiguration> azureConfig)
    {
        _calculatorService = calculatorService;
        _storageService = storageService;
        _configuration = configuration;
        _logger = logger;
        _azureConfig = azureConfig.Value;
        
        // Initialize ARM client for Azure Resource queries
        TokenCredential credential = _azureConfig.Authentication.Type == "ServicePrincipal"
            ? new ClientSecretCredential(_azureConfig.TenantId, _azureConfig.Authentication.ServicePrincipal.ClientId, _azureConfig.Authentication.ServicePrincipal.ClientSecret)
            : new DefaultAzureCredential();
        _armClient = new ArmClient(credential);
    }

    [HttpPost("analyze/platform")]
    public async Task<IActionResult> AnalyzePlatform([FromBody] PlatformEnergyRequest request)
    {
        try
        {
            _logger.LogInformation("Analysis request received: {Count} microservices, Date range: {Start} to {End}", 
                request.Microservices?.Count ?? 0, request.StartTime, request.EndTime);
            
            // Debug: Log each microservice received
            if (request.Microservices?.Any() == true)
            {
                foreach (var ms in request.Microservices)
                {
                    _logger.LogInformation("Microservice: {Name}, AppService: {AppServiceId}, Valid: {Valid}", 
                        ms.MicroserviceName, 
                        ms.AppServiceResourceId ?? "NULL", 
                        !string.IsNullOrEmpty(ms.AppServiceResourceId));
                }
            }
            
            // Calculate dynamic energy consumption based on time range
            var timeSpan = request.EndTime - request.StartTime;
            var daysInRange = Math.Max(1, timeSpan.TotalDays);
            
            // Handle direct Azure subscription analysis when no microservices are found
            if (request.AnalyzeAllResources && (request.Microservices?.Count == 0 || 
                request.Microservices?.All(ms => string.IsNullOrEmpty(ms.AppServiceResourceId)) == true))
            {
                _logger.LogInformation("Analyzing all Azure subscription resources directly (no microservices found)");
                return await AnalyzeAllSubscriptionResources(request);
            }
            
            // Validate microservices - must have App Service to be considered a microservice
            var validMicroservices = (request.Microservices ?? new List<MicroserviceResourceGroup>())
                .Where(ms => !string.IsNullOrEmpty(ms.AppServiceResourceId))
                .ToList();
            
            // Calculate actual utilization from Azure metrics (mock for now, can be replaced with real Azure Monitor)
            var actualUtilization = await CalculateActualUtilization(validMicroservices, request.StartTime, request.EndTime);
            var utilizationFactor = actualUtilization / 100.0;
            
            _logger.LogInformation("Analysis request: {Days} days, Calculated utilization: {Utilization}%, Microservices: {Count}", 
                daysInRange, actualUtilization, validMicroservices.Count);
            
            var energyReport = new EnergyReport
            {
                KilowattHours = 0, // Will be calculated based on resources
                ResourceType = "Platform",
                UtilizationPercentage = actualUtilization,
                Details = new Dictionary<string, double>()
            };

            double totalEnergy = 0;

            // Calculate energy for each valid microservice using parallel processing for better performance
            if (validMicroservices.Any())
            {
                var microserviceCalculations = validMicroservices.AsParallel().Select(microservice =>
                {
                    var microserviceEnergy = 0.0;
                    var details = new Dictionary<string, double>();
                    
                    // App Service energy calculation (guaranteed to exist for valid microservices)
                    var appServiceEnergy = CalculateAppServiceEnergy(microservice.AppServiceResourceId, daysInRange, utilizationFactor);
                    details[$"{microservice.MicroserviceName}_AppService"] = Math.Round(appServiceEnergy, 2);
                    microserviceEnergy += appServiceEnergy;
                    
                    // Function Apps energy calculation
                    var functionAppsEnergy = microservice.FunctionAppResourceIds?.Any() == true
                        ? CalculateFunctionAppsEnergy(microservice.FunctionAppResourceIds, daysInRange, utilizationFactor)
                        : 0.0;
                    details[$"{microservice.MicroserviceName}_Functions"] = Math.Round(functionAppsEnergy, 2);
                    microserviceEnergy += functionAppsEnergy;
                    
                    // Service Bus energy calculation
                    var serviceBusEnergy = microservice.ServiceBusResourceIds?.Any() == true
                        ? CalculateServiceBusEnergy(microservice.ServiceBusResourceIds, daysInRange, utilizationFactor)
                        : 0.0;
                    details[$"{microservice.MicroserviceName}_ServiceBus"] = Math.Round(serviceBusEnergy, 2);
                    microserviceEnergy += serviceBusEnergy;
                    
                    // Database energy calculation
                    var databaseEnergy = microservice.DatabaseResourceIds?.Any() == true
                        ? CalculateDatabaseEnergy(microservice.DatabaseResourceIds, daysInRange, utilizationFactor)
                        : 0.0;
                    details[$"{microservice.MicroserviceName}_Database"] = Math.Round(databaseEnergy, 2);
                    microserviceEnergy += databaseEnergy;
                    
                    return new { Name = microservice.MicroserviceName, Energy = microserviceEnergy, Details = details };
                }).ToList();
                
                // Aggregate results from parallel calculations
                foreach (var calc in microserviceCalculations)
                {
                    totalEnergy += calc.Energy;
                    foreach (var detail in calc.Details)
                    {
                        energyReport.Details[detail.Key] = detail.Value;
                    }
                }
            }

            // Calculate shared resources energy with detailed breakdown
            if (request.SharedResourceIds.Any())
            {
                var sharedResourcesBreakdown = CalculateSharedResourcesBreakdown(request.SharedResourceIds, daysInRange, utilizationFactor);
                
                foreach (var sharedResource in sharedResourcesBreakdown)
                {
                    energyReport.Details[sharedResource.Key] = sharedResource.Value;
                    totalEnergy += sharedResource.Value;
                }
            }

            // Set totals
            energyReport.KilowattHours = Math.Round(totalEnergy, 2);
            
            // Apply grid intensity factor for carbon calculation (region-specific)
            var gridIntensityFactor = GetGridIntensityFactor("West Europe"); // Default to West Europe
            energyReport.CarbonKg = Math.Round(totalEnergy * gridIntensityFactor, 2);

            // Sync the Details dictionary to JSON for storage
            energyReport.SyncDetailsToJson();
            
            // Debug logging to see what we're sending to frontend
            _logger.LogInformation("Energy report details count: {Count}, Total energy: {Energy}", 
                energyReport.Details.Count, energyReport.KilowattHours);

            // Generate historical trends data for the date range (optimized for performance)
            var trends = GenerateHistoricalTrends(request, totalEnergy);
            
            // Generate a unique report ID
            var reportId = $"platform_analysis_{DateTime.UtcNow:yyyyMMdd_HHmmss}";
            
            // Async storage save (don't wait for it to complete for better performance)
            _ = Task.Run(async () =>
            {
                try
                {
                    await _storageService.SaveReportToTable(energyReport);
                    _logger.LogInformation("Report {ReportId} saved to storage successfully", reportId);
                }
                catch (Exception storageEx)
                {
                    _logger.LogWarning(storageEx, "Failed to save report {ReportId} to storage", reportId);
                }
            });

            // Set response headers for performance
            Response.Headers["Cache-Control"] = "public, max-age=300"; // 5 minutes cache
            Response.Headers["X-Performance-Optimized"] = "true";

            return Ok(new { 
                ReportId = reportId, 
                EnergyReport = new {
                    // Convert to frontend-expected format (camelCase)
                    kilowattHours = energyReport.KilowattHours,
                    carbonKg = energyReport.CarbonKg,
                    resourceType = energyReport.ResourceType,
                    resourceName = energyReport.ResourceName,
                    utilizationPercentage = energyReport.UtilizationPercentage,
                    details = energyReport.Details, // This is the key property for the chart
                    // Add additional frontend-compatible properties
                    totalEnergyConsumption = energyReport.KilowattHours
                },
                Trends = trends,
                OptimizationRecommendations = GenerateOptimizationRecommendations(energyReport, actualUtilization),
                PerformanceMetrics = new {
                    ProcessingTimeMs = 100, // Placeholder - actual timing would need start time tracking
                    MicroservicesProcessed = validMicroservices.Count,
                    TotalResourcesAnalyzed = validMicroservices.Sum(ms => 1 + ms.FunctionAppResourceIds.Count + ms.ServiceBusResourceIds.Count + ms.DatabaseResourceIds.Count)
                },
                Note = "Optimized energy analysis with parallel processing for hackathon performance."
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing platform energy consumption: {Message}", ex.Message);
            return StatusCode(500, new { error = "Error analyzing platform energy consumption", details = ex.Message });
        }
    }

    private object GenerateHistoricalTrends(PlatformEnergyRequest request, double totalEnergy)
    {
        var startDate = request.StartTime;
        var endDate = request.EndTime;
        var totalDays = (endDate - startDate).TotalDays;

        // Optimize for performance: Limit data points for large date ranges
        var dailyTrends = GenerateDailyTrends(request, totalEnergy, startDate, totalDays);
        var weeklyTrends = GenerateWeeklyTrends(request, totalEnergy, startDate, totalDays);
        var monthlyTrends = GenerateMonthlyTrends(request, totalEnergy, startDate, totalDays);

        return new
        {
            daily = dailyTrends,
            weekly = weeklyTrends,
            monthly = monthlyTrends
        };
    }

    private List<object> GenerateDailyTrends(PlatformEnergyRequest request, double totalEnergy, DateTime startDate, double totalDays)
    {
        var trends = new List<object>();
        
        // Performance optimization: Sample data for large ranges
        var daysToGenerate = totalDays > 90 ? 90 : (int)Math.Max(7, totalDays); // Max 90 days for performance
        var samplingInterval = totalDays > 90 ? totalDays / 90 : 1; // Sample every N days for large ranges
        
        var dailyBaseEnergy = totalEnergy / Math.Max(totalDays, daysToGenerate);
        
        for (int i = 0; i < daysToGenerate; i++)
        {
            var actualDayOffset = i * samplingInterval;
            var date = startDate.AddDays(actualDayOffset);
            
            // Simple sine wave for variation - much faster than complex calculations
            var variation = 0.8 + (0.4 * Math.Sin(i * 0.1));
            var dailyEnergy = Math.Round(dailyBaseEnergy * variation, 2);
            
            // Pre-calculate microservice split to avoid repeated calculations
            var microserviceEnergy = Math.Round(dailyEnergy / Math.Max(request.Microservices.Count, 1), 2);
            
            trends.Add(new
            {
                date = date.ToString("yyyy-MM-dd"),
                totalEnergy = dailyEnergy,
                microservices = request.Microservices.ToDictionary(
                    ms => ms.MicroserviceName,
                    ms => microserviceEnergy
                )
            });
        }
        
        return trends;
    }

    private List<object> GenerateWeeklyTrends(PlatformEnergyRequest request, double totalEnergy, DateTime startDate, double totalDays)
    {
        var trends = new List<object>();
        
        // Performance optimization: Max 52 weeks (1 year)
        var weeksToGenerate = Math.Min((int)Math.Ceiling(totalDays / 7.0), 52);
        var weeklyBaseEnergy = (totalEnergy / Math.Max(totalDays, 1)) * 7; // Weekly energy
        
        for (int week = 0; week < weeksToGenerate; week++)
        {
            var weekStart = startDate.AddDays(week * 7);
            var variation = 0.85 + (0.3 * Math.Sin(week * 0.2));
            var weeklyEnergy = Math.Round(weeklyBaseEnergy * variation, 2);
            
            // Pre-calculate microservice distribution
            var microserviceEnergy = Math.Round(weeklyEnergy / Math.Max(request.Microservices.Count, 1), 2);
            
            trends.Add(new
            {
                date = weekStart.ToString("yyyy-MM-dd"),
                totalEnergy = weeklyEnergy,
                microservices = request.Microservices.ToDictionary(
                    ms => ms.MicroserviceName,
                    ms => microserviceEnergy
                )
            });
        }
        
        return trends;
    }

    private List<object> GenerateMonthlyTrends(PlatformEnergyRequest request, double totalEnergy, DateTime startDate, double totalDays)
    {
        var trends = new List<object>();
        
        // Performance optimization: Max 24 months (2 years)
        var monthsToGenerate = Math.Min((int)Math.Ceiling(totalDays / 30.0), 24);
        var monthlyBaseEnergy = (totalEnergy / Math.Max(totalDays, 1)) * 30; // Monthly energy
        
        var baseDate = new DateTime(startDate.Year, startDate.Month, 1);
        
        for (int month = 0; month < monthsToGenerate; month++)
        {
            var currentMonth = baseDate.AddMonths(month);
            var variation = 0.9 + (0.2 * Math.Sin(month * 0.3));
            var monthlyEnergy = Math.Round(monthlyBaseEnergy * variation, 2);
            
            // Pre-calculate microservice distribution
            var microserviceEnergy = Math.Round(monthlyEnergy / Math.Max(request.Microservices.Count, 1), 2);
            
            trends.Add(new
            {
                date = currentMonth.ToString("yyyy-MM-dd"),
                totalEnergy = monthlyEnergy,
                microservices = request.Microservices.ToDictionary(
                    ms => ms.MicroserviceName,
                    ms => microserviceEnergy
                )
            });
        }
        
        return trends;
    }

    [HttpPost("analyze/azure-resources")]
    public async Task<IActionResult> AnalyzeAzureResources([FromBody] AzureResourceEnergyRequest request)
    {
        try
        {
            var report = await _calculatorService.CalculateAzureResourceEnergy(
                request.FunctionAppResourceId,
                request.ServiceBusResourceId,
                request.MongoDbResourceId,
                request.StartTime,
                request.EndTime);

            var energyReport = new EnergyReport
            {
                KilowattHours = report.TotalEnergy,
                CarbonKg = report.TotalEnergy * 0.3, // Assuming West Europe region
                ResourceType = "AzureResources",
                UtilizationPercentage = 100,
                Details = new Dictionary<string, double>
                {
                    { "FunctionApp", report.FunctionAppEnergy },
                    { "ServiceBus", report.ServiceBusEnergy },
                    { "MongoDb", report.MongoDbEnergy }
                }
            };

            var blobName = await _storageService.SaveReportToBlob(energyReport);
            await _storageService.SaveReportToTable(energyReport);

            return Ok(new { ReportId = blobName, Report = report });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing Azure resources");
            return StatusCode(500, "Error analyzing Azure resources");
        }
    }
    
    #region Energy Calculation Methods - Based on Baseline Formula
    
    /// <summary>
    /// Calculate App Service energy using VM size mapping and utilization
    /// Formula: VM TDP (W) * utilization * hours / 1000 = kWh
    /// </summary>
    private double CalculateAppServiceEnergy(string resourceId, double daysInRange, double utilizationFactor)
    {
        _logger.LogInformation("🔧 CalculateAppServiceEnergy called for {ResourceId}", resourceId);
        
        // Get the actual creation date of the App Service
        var actualDaysInRange = GetActualServiceDaysInRange(resourceId, daysInRange);
        
        // Map App Service plan to equivalent VM size
        var vmSize = GetAppServiceVmEquivalent(resourceId);
        var nominalTdpWatts = GetNominalTdp(vmSize);
        
        // Calculate energy: Watts * hours * utilization / 1000 = kWh
        var hoursInRange = actualDaysInRange * 24;
        var energyKwh = (nominalTdpWatts * hoursInRange * utilizationFactor) / 1000.0;
        
        // Enhanced logging to help debug tier detection
        var detectedTier = GetAppServiceTierFromResourceId(resourceId);
        _logger.LogInformation("🎯 App Service {ResourceId}: Detected Tier={Tier}, VM Size={VmSize}, TDP={TdpWatts}W, Actual Days={ActualDays}, Hours={Hours}, Utilization={Utilization}, Energy={Energy} kWh",
            resourceId, detectedTier, vmSize, nominalTdpWatts, actualDaysInRange, hoursInRange, utilizationFactor, energyKwh);
            
        return energyKwh;
    }
    
    /// <summary>
    /// Get the actual number of days a service was running within the analysis date range
    /// </summary>
    private double GetActualServiceDaysInRange(string resourceId, double requestedDaysInRange)
    {
        try
        {
            // Try to get the actual creation date from Azure
            var creationDate = GetResourceCreationDate(resourceId);
            
            if (creationDate.HasValue)
            {
                var analysisEndDate = DateTime.UtcNow;
                var analysisStartDate = analysisEndDate.AddDays(-requestedDaysInRange);
                
                // If service was created after analysis start date, calculate from creation date
                if (creationDate.Value > analysisStartDate)
                {
                    var actualDays = (analysisEndDate - creationDate.Value).TotalDays;
                    _logger.LogInformation("🕒 Service {ResourceId} created on {CreationDate}, actual runtime: {ActualDays} days (vs requested {RequestedDays} days)", 
                        resourceId, creationDate.Value.ToString("yyyy-MM-dd"), actualDays, requestedDaysInRange);
                    return Math.Max(0, actualDays);
                }
                else
                {
                    _logger.LogInformation("✅ Service {ResourceId} existed for full analysis period ({Days} days)", resourceId, requestedDaysInRange);
                    return requestedDaysInRange;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "⚠️ Could not determine creation date for {ResourceId}, using full date range", resourceId);
        }
        
        // Fallback to full date range if we can't determine creation date
        return requestedDaysInRange;
    }
    
    /// <summary>
    /// Get the creation date of an Azure resource (with caching)
    /// </summary>
    private DateTime? GetResourceCreationDate(string resourceId)
    {
        // Check cache first
        if (_creationDateCache.TryGetValue(resourceId, out DateTime? cachedDate))
        {
            _logger.LogInformation("⚡ Retrieved creation date from cache for {ResourceId}: {CreationDate}", resourceId, cachedDate);
            return cachedDate;
        }

        try
        {
            _logger.LogInformation("🕐 Querying creation date for {ResourceId}", resourceId);
            
            var resource = _armClient.GetGenericResource(new Azure.Core.ResourceIdentifier(resourceId));
            var resourceData = resource.Get();
            
            DateTime? resultDate = null;
            
            // Try to get creation timestamp from resource properties
            var properties = resourceData.Value.Data.Properties;
            if (properties != null)
            {
                var propertiesJson = JsonDocument.Parse(properties.ToString());
                
                // Look for creation timestamp in various possible fields
                var possibleDateFields = new[] { "createdTime", "creationTime", "created", "timeCreated", "provisioningTime" };
                
                foreach (var field in possibleDateFields)
                {
                    if (propertiesJson.RootElement.TryGetProperty(field, out var dateElement))
                    {
                        if (DateTime.TryParse(dateElement.GetString(), out var creationDate))
                        {
                            resultDate = creationDate.ToUniversalTime();
                            _logger.LogInformation("✅ Found creation date for {ResourceId}: {CreationDate}", resourceId, resultDate);
                            break;
                        }
                    }
                }
            }
            
            // Fallback: Try to get from system data (last modified as proxy for creation)
            if (!resultDate.HasValue)
            {
                var systemData = resourceData.Value.Data.SystemData;
                if (systemData?.CreatedOn.HasValue == true)
                {
                    resultDate = systemData.CreatedOn.Value.UtcDateTime;
                    _logger.LogInformation("✅ Found system creation date for {ResourceId}: {CreationDate}", resourceId, resultDate);
                }
            }
            
            if (!resultDate.HasValue)
            {
                _logger.LogWarning("⚠️ No creation date found in resource properties for {ResourceId}", resourceId);
            }
            
            // Cache the result (even if null)
            _creationDateCache[resourceId] = resultDate;
            return resultDate;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "❌ Failed to query creation date for {ResourceId}", resourceId);
            // Cache null result to avoid repeated failures
            _creationDateCache[resourceId] = null;
        }
        
        return null;
    }
    
    /// <summary>
    /// Extract App Service tier information from resource ID for debugging
    /// </summary>
    private string GetAppServiceTierFromResourceId(string resourceId)
    {
        // First try to get the actual SKU from Azure
        try
        {
            var actualSku = GetAppServicePlanSku(resourceId);
            if (!string.IsNullOrEmpty(actualSku))
            {
                return $"Actual SKU: {actualSku}";
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not retrieve actual SKU for {ResourceId}", resourceId);
        }

        // Fallback to pattern matching
        var resourceIdLower = resourceId.ToLower();
        
        // Check for specific SKU patterns
        if (resourceIdLower.Contains("p3v")) return "Premium P3V2/V3 (pattern match)";
        if (resourceIdLower.Contains("p2v")) return "Premium P2V2/V3 (pattern match)";
        if (resourceIdLower.Contains("p1v")) return "Premium P1V2/V3 (pattern match)";
        if (resourceIdLower.Contains("s3")) return "Standard S3 (pattern match)";
        if (resourceIdLower.Contains("s2")) return "Standard S2 (pattern match)";
        if (resourceIdLower.Contains("s1")) return "Standard S1 (pattern match)";
        if (resourceIdLower.Contains("b3")) return "Basic B3 (pattern match)";
        if (resourceIdLower.Contains("b2")) return "Basic B2 (pattern match)";
        if (resourceIdLower.Contains("b1")) return "Basic B1 (pattern match)";
        
        // Check for general tier names
        if (resourceIdLower.Contains("premium")) return "Premium (general pattern match)";
        if (resourceIdLower.Contains("standard")) return "Standard (general pattern match)";
        if (resourceIdLower.Contains("basic")) return "Basic (general pattern match)";
        
        return "Unknown/Default";
    }
    
    /// <summary>
    /// Calculate Function Apps energy based on consumption model
    /// </summary>
    private double CalculateFunctionAppsEnergy(List<string> functionAppResourceIds, double daysInRange, double utilizationFactor)
    {
        var totalEnergyKwh = 0.0;
        
        foreach (var resourceId in functionAppResourceIds)
        {
            // Function Apps have different energy profile - consumption-based
            // Estimated 20W baseline + execution energy
            var baselineWatts = 20.0; // Baseline compute for function host
            var executionWatts = 50.0 * utilizationFactor; // Additional watts during execution
            
            var hoursInRange = daysInRange * 24;
            var energyKwh = ((baselineWatts + executionWatts) * hoursInRange) / 1000.0;
            
            totalEnergyKwh += energyKwh;
            
            _logger.LogInformation("Function App {ResourceId}: Baseline={BaselineWatts}W, Execution={ExecutionWatts}W, Energy={Energy} kWh",
                resourceId, baselineWatts, executionWatts, energyKwh);
        }
        
        return totalEnergyKwh;
    }
    
    /// <summary>
    /// Calculate Service Bus energy based on message processing
    /// </summary>
    private double CalculateServiceBusEnergy(List<string> serviceBusResourceIds, double daysInRange, double utilizationFactor)
    {
        var totalEnergyKwh = 0.0;
        
        foreach (var resourceId in serviceBusResourceIds)
        {
            // Service Bus energy - minimal compute, mostly network and storage
            var baselineWatts = 15.0; // Baseline for service bus namespace
            var processingWatts = 25.0 * utilizationFactor; // Additional for message processing
            
            var hoursInRange = daysInRange * 24;
            var energyKwh = ((baselineWatts + processingWatts) * hoursInRange) / 1000.0;
            
            totalEnergyKwh += energyKwh;
            
            _logger.LogInformation("Service Bus {ResourceId}: Baseline={BaselineWatts}W, Processing={ProcessingWatts}W, Energy={Energy} kWh",
                resourceId, baselineWatts, processingWatts, energyKwh);
        }
        
        return totalEnergyKwh;
    }
    
    /// <summary>
    /// Calculate Database energy based on DTU/compute size
    /// </summary>
    private double CalculateDatabaseEnergy(List<string> databaseResourceIds, double daysInRange, double utilizationFactor)
    {
        var totalEnergyKwh = 0.0;
        
        foreach (var resourceId in databaseResourceIds)
        {
            // Database energy based on tier - map to equivalent compute
            var databaseTier = GetDatabaseTier(resourceId);
            var equivalentWatts = GetDatabaseWattage(databaseTier);
            
            var hoursInRange = daysInRange * 24;
            var energyKwh = (equivalentWatts * hoursInRange * utilizationFactor) / 1000.0;
            
            totalEnergyKwh += energyKwh;
            
            _logger.LogInformation("Database {ResourceId}: Tier={Tier}, Watts={Watts}W, Energy={Energy} kWh",
                resourceId, databaseTier, equivalentWatts, energyKwh);
        }
        
        return totalEnergyKwh;
    }
    
    /// <summary>
    /// Get grid intensity factor for carbon calculation by region
    /// </summary>
    private double GetGridIntensityFactor(string region)
    {
        // Grid intensity factors (kg CO2 per kWh) by region
        var gridFactors = new Dictionary<string, double>
        {
            { "West Europe", 0.24 },        // Netherlands - lower carbon intensity
            { "North Europe", 0.18 },       // Ireland - renewable energy
            { "East US", 0.45 },            // US East - mixed grid
            { "West US", 0.35 },            // US West - some renewables
            { "Southeast Asia", 0.65 },     // Singapore - higher carbon intensity
            { "Australia East", 0.85 },     // Australia - coal-heavy grid
            { "UK South", 0.22 },           // UK - good renewable mix
            { "Germany West Central", 0.38 }, // Germany - transitioning grid
            { "France Central", 0.06 },     // France - nuclear-heavy grid
            { "Japan East", 0.52 },         // Japan - mixed sources
            { "Brazil South", 0.12 },       // Brazil - hydro-heavy
            { "Canada Central", 0.15 },     // Canada - hydro + renewable
            { "Norway East", 0.02 }         // Norway - almost all renewable
        };
        
        return gridFactors.TryGetValue(region, out var factor) ? factor : 0.30; // Default global average
    }
    
    /// <summary>
    /// Map App Service plan to equivalent VM size
    /// </summary>
    private string GetAppServiceVmEquivalent(string resourceId)
    {
        _logger.LogInformation("🔍 GetAppServiceVmEquivalent called for {ResourceId}", resourceId);
        
        try
        {
            // Try to get the actual App Service Plan SKU from Azure
            var actualSku = GetAppServicePlanSku(resourceId);
            if (!string.IsNullOrEmpty(actualSku))
            {
                var vmSize = MapSkuToVmSize(actualSku);
                _logger.LogInformation("✅ Real SKU detected: {Sku} -> {VmSize}", actualSku, vmSize);
                return vmSize;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "❌ Failed to query App Service Plan SKU for {ResourceId}, falling back to pattern matching", resourceId);
        }

        // Enhanced fallback: analyze resource group patterns for different tiers
        _logger.LogInformation("⚠️ Using enhanced pattern matching for {ResourceId}", resourceId);
        var resourceIdLower = resourceId.ToLower();
        
        // Based on ACTUAL App Service Plan information provided:
        // - energycalc-api: Basic B1 plan (lowest tier)
        // - payments service: Higher tier than B1
        
        // Energy resource group - ACTUALLY Basic B1 plan (corrected)
        if (resourceIdLower.Contains("energy-dev") || resourceIdLower.Contains("energycalc"))
        {
            _logger.LogInformation("🎯 Energy service detected - using B1 tier (Basic - ACTUAL tier)");
            return "Standard_B1ms"; // B1 equivalent: 1 core, 2GB RAM, 35W TDP
        }
        
        // Payment services - Higher tier than energy service
        if (resourceIdLower.Contains("energy-dev-payments") || resourceIdLower.Contains("payments"))
        {
            _logger.LogInformation("🎯 Payments service detected - using S1 tier (Standard - higher than B1)");
            return "Standard_B2ms"; // S1 equivalent: 1 core, 3.5GB RAM, 65W TDP  
        }
        
        // Auth services - Moderate tier
        if (resourceIdLower.Contains("energy-dev-auth") || resourceIdLower.Contains("-auth"))
        {
            _logger.LogInformation("🎯 Auth service detected - using S2 tier (Standard)");
            return "Standard_D2_v3"; // S2 equivalent: 2 cores, 7GB RAM, 85W TDP
        }
        
        // User management services - Moderate tier
        if (resourceIdLower.Contains("energy-dev-users") || resourceIdLower.Contains("-users"))
        {
            _logger.LogInformation("🎯 Users service detected - using S1 tier (Standard)");
            return "Standard_B2ms"; // S1 equivalent: 1 core, 3.5GB RAM, 65W TDP
        }
        
        // Admin and portal services - Higher tier for admin operations  
        if (resourceIdLower.Contains("energy-dev-admin") || resourceIdLower.Contains("portal") || resourceIdLower.Contains("bff"))
        {
            _logger.LogInformation("🎯 Admin/Portal service detected - using S2 tier");
            return "Standard_D2_v3"; // S2 equivalent: 2 cores, 7GB RAM, 85W TDP
        }
        
        // Session and watchdog services - Basic tier like energy
        if (resourceIdLower.Contains("energy-dev-sessions") || resourceIdLower.Contains("energy-dev-watchdog"))
        {
            _logger.LogInformation("🎯 Session/Watchdog service detected - using B1 tier (Basic)");
            return "Standard_B1ms"; // B1 equivalent: 1 core, 2GB RAM, 35W TDP
        }
        
        // Check for Premium V2/V3 App Service Plans (P1V2, P2V2, P3V2, P1V3, P2V3, P3V3)
        if (resourceIdLower.Contains("p3v") || resourceIdLower.Contains("premium-v3-large") || 
            resourceIdLower.Contains("p3-v3") || resourceIdLower.Contains("p3_v3"))
        {
            _logger.LogInformation("🎯 P3V3 tier detected from resource name");
            return "Standard_D8_v3"; // High-performance: 8 cores, 32GB RAM
        }
            
        if (resourceIdLower.Contains("p2v") || resourceIdLower.Contains("premium-v2-medium") ||
            resourceIdLower.Contains("p2-v2") || resourceIdLower.Contains("p2_v2"))
        {
            _logger.LogInformation("🎯 P2V2 tier detected from resource name");
            return "Standard_D4_v3"; // Mid-high performance: 4 cores, 16GB RAM
        }
            
        if (resourceIdLower.Contains("p1v") || resourceIdLower.Contains("premium-v1-small") ||
            resourceIdLower.Contains("p1-v1") || resourceIdLower.Contains("p1_v1"))
        {
            _logger.LogInformation("🎯 P1V2 tier detected from resource name");
            return "Standard_D2_v3"; // Premium entry: 2 cores, 8GB RAM
        }
        
        // Check for Standard App Service Plans (S1, S2, S3)
        if (resourceIdLower.Contains("s3") || resourceIdLower.Contains("standard-large") ||
            resourceIdLower.Contains("standard_s3"))
        {
            _logger.LogInformation("🎯 S3 tier detected from resource name");
            return "Standard_D4_v3"; // Standard large: 4 cores, 14GB RAM
        }
            
        if (resourceIdLower.Contains("s2") || resourceIdLower.Contains("standard-medium") ||
            resourceIdLower.Contains("standard_s2"))
        {
            _logger.LogInformation("🎯 S2 tier detected from resource name");
            return "Standard_D2_v3"; // Standard medium: 2 cores, 7GB RAM
        }
            
        if (resourceIdLower.Contains("s1") || resourceIdLower.Contains("standard-small") ||
            resourceIdLower.Contains("standard_s1"))
        {
            _logger.LogInformation("🎯 S1 tier detected from resource name");
            return "Standard_B2ms"; // Standard small: 2 cores, 8GB RAM
        }
        
        // Check for Basic App Service Plans (B1, B2, B3)
        if (resourceIdLower.Contains("b3") || resourceIdLower.Contains("basic-large") ||
            resourceIdLower.Contains("basic_b3"))
        {
            _logger.LogInformation("🎯 B3 tier detected from resource name");
            return "Standard_B4ms"; // Basic large: 4 cores, 16GB RAM
        }
            
        if (resourceIdLower.Contains("b2") || resourceIdLower.Contains("basic-medium") ||
            resourceIdLower.Contains("basic_b2"))
        {
            _logger.LogInformation("🎯 B2 tier detected from resource name");
            return "Standard_B2ms"; // Basic medium: 2 cores, 8GB RAM
        }
            
        if (resourceIdLower.Contains("b1") || resourceIdLower.Contains("basic-small") ||
            resourceIdLower.Contains("basic_b1"))
        {
            _logger.LogInformation("🎯 B1 tier detected from resource name");
            return "Standard_B1ms"; // Basic small: 1 core, 2GB RAM
        }
        
        // Legacy checks for naming patterns
        if (resourceIdLower.Contains("premium"))
        {
            _logger.LogInformation("🎯 Premium tier detected from resource name");
            return "Standard_D4_v3"; // Default premium
        }
        else if (resourceIdLower.Contains("standard"))
        {
            _logger.LogInformation("🎯 Standard tier detected from resource name");
            return "Standard_D2_v3"; // Default standard
        }
        else if (resourceIdLower.Contains("basic"))
        {
            _logger.LogInformation("🎯 Basic tier detected from resource name");
            return "Standard_B2ms"; // Default basic
        }
            
        // Default fallback for unknown services
        _logger.LogWarning("⚠️ Could not determine App Service tier for {ResourceId}, defaulting to Standard_B2ms", resourceId);
        return "Standard_B2ms"; // Conservative default
    }

    /// <summary>
    /// Query Azure to get the actual App Service Plan SKU (with caching)
    /// </summary>
    private string GetAppServicePlanSku(string appServiceResourceId)
    {
        // Check cache first
        if (_skuCache.TryGetValue(appServiceResourceId, out string? cachedSku) && !string.IsNullOrEmpty(cachedSku))
        {
            _logger.LogInformation("⚡ Retrieved SKU from cache for {ResourceId}: {Sku}", appServiceResourceId, cachedSku);
            return cachedSku;
        }

        try
        {
            _logger.LogInformation("🔍 Starting SKU query for App Service: {ResourceId}", appServiceResourceId);
            
            // Get the App Service resource from Azure
            var appServiceResource = _armClient.GetGenericResource(new Azure.Core.ResourceIdentifier(appServiceResourceId));
            _logger.LogInformation("🌐 ARM client created resource identifier successfully");
            
            var appServiceData = appServiceResource.Get();
            _logger.LogInformation("📥 Retrieved App Service data from Azure");
            
            // Parse the properties as JSON to get the server farm ID
            var properties = appServiceData.Value.Data.Properties;
            if (properties == null)
            {
                _logger.LogWarning("⚠️ App Service properties are null");
                _skuCache[appServiceResourceId] = ""; // Cache empty result
                return "";
            }
            
            _logger.LogInformation("📋 App Service properties retrieved, parsing JSON...");
            var propertiesJson = JsonDocument.Parse(properties.ToString());
            
            if (!propertiesJson.RootElement.TryGetProperty("serverFarmId", out var serverFarmElement))
            {
                _logger.LogWarning("⚠️ serverFarmId property not found in App Service properties");
                _skuCache[appServiceResourceId] = ""; // Cache empty result
                return "";
            }
            
            var serverFarmId = serverFarmElement.GetString();
            if (string.IsNullOrEmpty(serverFarmId))
            {
                _logger.LogWarning("⚠️ serverFarmId is null or empty");
                _skuCache[appServiceResourceId] = ""; // Cache empty result
                return "";
            }
            
            _logger.LogInformation("🏭 Found serverFarmId: {ServerFarmId}", serverFarmId);
            
            // Get the App Service Plan resource
            var planResource = _armClient.GetGenericResource(new Azure.Core.ResourceIdentifier(serverFarmId));
            var planData = planResource.Get();
            _logger.LogInformation("📥 Retrieved App Service Plan data from Azure");
            
            // Extract SKU from the plan properties
            var planProperties = planData.Value.Data.Properties;
            if (planProperties == null)
            {
                _logger.LogWarning("⚠️ App Service Plan properties are null");
                _skuCache[appServiceResourceId] = ""; // Cache empty result
                return "";
            }
            
            _logger.LogInformation("📋 App Service Plan properties retrieved, parsing for SKU...");
            var planPropertiesJson = JsonDocument.Parse(planProperties.ToString());
            
            if (!planPropertiesJson.RootElement.TryGetProperty("sku", out var skuElement))
            {
                _logger.LogWarning("⚠️ sku property not found in App Service Plan properties");
                _skuCache[appServiceResourceId] = ""; // Cache empty result
                return "";
            }
            
            if (!skuElement.TryGetProperty("name", out var nameElement))
            {
                _logger.LogWarning("⚠️ sku.name property not found in App Service Plan");
                _skuCache[appServiceResourceId] = ""; // Cache empty result
                return "";
            }
            
            var skuName = nameElement.GetString() ?? "";
            _logger.LogInformation("✅ Successfully found App Service Plan SKU: {Sku} for {ResourceId}", skuName, appServiceResourceId);
            
            // Cache the result
            _skuCache[appServiceResourceId] = skuName;
            return skuName;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Exception in GetAppServicePlanSku for {ResourceId}: {Message}", appServiceResourceId, ex.Message);
            _logger.LogError("❌ Full exception details: {ExceptionType} - {StackTrace}", ex.GetType().Name, ex.StackTrace);
            
            // Cache empty result to avoid repeated failures
            _skuCache[appServiceResourceId] = "";
        }
        
        return "";
    }

    /// <summary>
    /// Map actual Azure App Service Plan SKU to equivalent VM size
    /// </summary>
    private string MapSkuToVmSize(string sku)
    {
        var skuUpper = sku.ToUpper();
        
        return skuUpper switch
        {
            // Basic tiers
            "B1" => "Standard_B1ms",    // 1 core, 1.75GB RAM
            "B2" => "Standard_B2ms",    // 2 cores, 3.5GB RAM  
            "B3" => "Standard_B4ms",    // 4 cores, 7GB RAM
            
            // Standard tiers
            "S1" => "Standard_B2ms",    // 1 core, 1.75GB RAM
            "S2" => "Standard_D2_v3",   // 2 cores, 3.5GB RAM
            "S3" => "Standard_D4_v3",   // 4 cores, 7GB RAM
            
            // Premium V1 tiers
            "P1" => "Standard_D2_v3",   // 1 core, 1.75GB RAM
            "P2" => "Standard_D4_v3",   // 2 cores, 3.5GB RAM
            "P3" => "Standard_D8_v3",   // 4 cores, 7GB RAM
            
            // Premium V2 tiers
            "P1V2" => "Standard_D2_v3",  // 1 core, 3.5GB RAM
            "P2V2" => "Standard_D4_v3",  // 2 cores, 7GB RAM
            "P3V2" => "Standard_D8_v3",  // 4 cores, 14GB RAM
            
            // Premium V3 tiers  
            "P1V3" => "Standard_D2_v3",  // 2 cores, 8GB RAM
            "P2V3" => "Standard_D4_v3",  // 4 cores, 16GB RAM
            "P3V3" => "Standard_D8_v3",  // 8 cores, 32GB RAM
            
            // Isolated tiers (very high performance)
            "I1" => "Standard_D4_v3",    // 1 core, 3.5GB RAM
            "I2" => "Standard_D8_v3",    // 2 cores, 7GB RAM
            "I3" => "Standard_D16_v3",   // 4 cores, 14GB RAM
            
            // Default fallback
            _ => "Standard_D2_v3"
        };
    }
    
    /// <summary>
    /// Get nominal TDP (Thermal Design Power) for VM sizes
    /// </summary>
    private double GetNominalTdp(string vmSize)
    {
        var tdpMap = new Dictionary<string, double>
        {
            // Basic tier (B-series) - Burstable VMs with lower baseline power
            { "Standard_B1s", 25.0 },     // 1 core, 1GB RAM - Very low power
            { "Standard_B1ms", 35.0 },    // 1 core, 2GB RAM - Low power
            { "Standard_B2s", 50.0 },     // 2 cores, 4GB RAM - Low power
            { "Standard_B2ms", 65.0 },    // 2 cores, 8GB RAM - Low-medium power
            { "Standard_B4ms", 130.0 },   // 4 cores, 16GB RAM - Medium power
            { "Standard_B8ms", 260.0 },   // 8 cores, 32GB RAM - High power
            
            // General Purpose (D-series) - Balanced compute and memory
            { "Standard_D2_v3", 85.0 },   // 2 cores, 8GB RAM - Standard workloads
            { "Standard_D4_v3", 170.0 },  // 4 cores, 16GB RAM - Medium workloads
            { "Standard_D8_v3", 340.0 },  // 8 cores, 32GB RAM - High workloads
            { "Standard_D16_v3", 680.0 }, // 16 cores, 64GB RAM - Very high workloads
            
            // Compute Optimized (F-series) - High CPU performance
            { "Standard_F2s_v2", 95.0 },  // 2 cores, 4GB RAM - CPU intensive
            { "Standard_F4s_v2", 190.0 }, // 4 cores, 8GB RAM - CPU intensive
            { "Standard_F8s_v2", 380.0 }, // 8 cores, 16GB RAM - CPU intensive
            
            // Memory Optimized (E-series) - High memory-to-core ratio
            { "Standard_E2_v3", 100.0 },  // 2 cores, 16GB RAM - Memory intensive
            { "Standard_E4_v3", 200.0 },  // 4 cores, 32GB RAM - Memory intensive
            { "Standard_E8_v3", 400.0 },  // 8 cores, 64GB RAM - Memory intensive
            
            // Legacy support for older VM sizes
            { "Standard_A1", 40.0 },      // Legacy - 1 core, 1.75GB RAM
            { "Standard_A2", 80.0 },      // Legacy - 2 cores, 3.5GB RAM
            { "Standard_A4", 160.0 },     // Legacy - 4 cores, 7GB RAM
        };
        
        if (tdpMap.TryGetValue(vmSize, out var tdp))
        {
            _logger.LogDebug("VM Size {VmSize} mapped to {TdpWatts}W TDP", vmSize, tdp);
            return tdp;
        }
        
        // If unknown VM size, estimate based on naming patterns
        if (vmSize.Contains("D8") || vmSize.Contains("8"))
            return 340.0; // 8-core default
        else if (vmSize.Contains("D4") || vmSize.Contains("4"))
            return 170.0; // 4-core default
        else if (vmSize.Contains("D2") || vmSize.Contains("2"))
            return 85.0;  // 2-core default
        else if (vmSize.Contains("B1") || vmSize.Contains("1"))
            return 35.0;  // 1-core default
            
        _logger.LogWarning("Unknown VM size {VmSize}, defaulting to 85W TDP", vmSize);
        return 85.0; // Default to Standard_D2_v3 equivalent
    }
    
    /// <summary>
    /// Estimate database tier from resource ID
    /// </summary>
    private string GetDatabaseTier(string resourceId)
    {
        // Parse resource ID or use naming conventions to determine tier
        if (resourceId.Contains("premium") || resourceId.Contains("Premium"))
            return "Premium";
        else if (resourceId.Contains("standard") || resourceId.Contains("Standard"))
            return "Standard";
        else
            return "Basic";
    }
    
    /// <summary>
    /// Get equivalent wattage for database tiers
    /// </summary>
    private double GetDatabaseWattage(string tier)
    {
        var wattageMap = new Dictionary<string, double>
        {
            { "Basic", 30.0 },      // Low-end compute
            { "Standard", 75.0 },   // Mid-tier compute  
            { "Premium", 150.0 },   // High-performance compute
            { "Hyperscale", 200.0 } // Distributed compute
        };
        
        return wattageMap.TryGetValue(tier, out var wattage) ? wattage : 75.0; // Default to Standard
    }
    
    /// <summary>
    /// Calculate detailed breakdown for shared resources instead of lumping them together
    /// </summary>
    private Dictionary<string, double> CalculateSharedResourcesBreakdown(List<string> sharedResourceIds, double daysInRange, double utilizationFactor)
    {
        var breakdown = new Dictionary<string, double>();
        
        foreach (var resourceId in sharedResourceIds)
        {
            var resourceType = GetSharedResourceType(resourceId);
            var resourceName = GetSharedResourceName(resourceId);
            var energyKwh = CalculateSharedResourceEnergy(resourceType, daysInRange, utilizationFactor);
            
            var detailKey = $"Shared_{resourceType}_{resourceName}";
            breakdown[detailKey] = Math.Round(energyKwh, 2);
            
            _logger.LogInformation("Shared Resource {ResourceId}: Type={Type}, Name={Name}, Energy={Energy} kWh",
                resourceId, resourceType, resourceName, energyKwh);
        }
        
        return breakdown;
    }
    
    /// <summary>
    /// Calculate energy for specific shared resource types
    /// </summary>
    private double CalculateSharedResourceEnergy(string resourceType, double daysInRange, double utilizationFactor)
    {
        var hoursInRange = daysInRange * 24;
        
        // Energy profiles for different shared resource types
        var resourceWattage = resourceType switch
        {
            "Storage" => 25.0,           // Storage accounts - low power
            "Redis" => 45.0,             // Redis cache - memory intensive
            "KeyVault" => 5.0,           // Key Vault - minimal compute
            "ApplicationInsights" => 15.0, // App Insights - monitoring
            "ServiceBus" => 30.0,        // Shared Service Bus namespace
            "CosmosDB" => 120.0,         // Cosmos DB - high performance
            "CDN" => 20.0,               // Content Delivery Network
            "LoadBalancer" => 35.0,      // Load Balancer - network processing
            "VirtualNetwork" => 10.0,    // Virtual Network - infrastructure
            "NetworkSecurityGroup" => 8.0, // NSG - security processing
            "PublicIP" => 3.0,           // Public IP - minimal overhead
            "TrafficManager" => 12.0,    // Traffic Manager - DNS routing
            _ => 40.0                    // Default for unknown resource types
        };
        
        return (resourceWattage * hoursInRange * utilizationFactor) / 1000.0;
    }
    
    /// <summary>
    /// Extract resource type from Azure resource ID for shared resources
    /// </summary>
    private string GetSharedResourceType(string resourceId)
    {
        if (resourceId.Contains("/Microsoft.Storage/"))
            return "Storage";
        else if (resourceId.Contains("/Microsoft.Cache/"))
            return "Redis";
        else if (resourceId.Contains("/Microsoft.KeyVault/"))
            return "KeyVault";
        else if (resourceId.Contains("/Microsoft.Insights/"))
            return "ApplicationInsights";
        else if (resourceId.Contains("/Microsoft.ServiceBus/"))
            return "ServiceBus";
        else if (resourceId.Contains("/Microsoft.DocumentDB/"))
            return "CosmosDB";
        else if (resourceId.Contains("/Microsoft.Cdn/"))
            return "CDN";
        else if (resourceId.Contains("/Microsoft.Network/loadBalancers"))
            return "LoadBalancer";
        else if (resourceId.Contains("/Microsoft.Network/virtualNetworks"))
            return "VirtualNetwork";
        else if (resourceId.Contains("/Microsoft.Network/networkSecurityGroups"))
            return "NetworkSecurityGroup";
        else if (resourceId.Contains("/Microsoft.Network/publicIPAddresses"))
            return "PublicIP";
        else if (resourceId.Contains("/Microsoft.Network/trafficManagerProfiles"))
            return "TrafficManager";
        else
            return "Infrastructure";
    }
    
    /// <summary>
    /// Extract a friendly name from Azure resource ID
    /// </summary>
    private string GetSharedResourceName(string resourceId)
    {
        // Extract the resource name from the resource ID
        // Azure resource IDs follow pattern: /subscriptions/{sub}/resourceGroups/{rg}/providers/{provider}/{type}/{name}
        var parts = resourceId.Split('/');
        if (parts.Length >= 2)
        {
            var resourceName = parts[^1]; // Last part is the resource name
            // Make it more readable
            return resourceName.Replace("-", " ").Replace("_", " ");
        }
        return "Unknown";
    }
    
    #endregion

    [HttpPost("analyze/infrastructure")]
    public async Task<IActionResult> AnalyzeInfrastructure()
    {
        try
        {
            // In a real implementation, we'd get this from Azure DevOps API
            var terraformJson = System.IO.File.ReadAllText("sample-terraform.json");
            var kWh = await _calculatorService.CalculateInfrastructureEnergy(terraformJson);

            var report = new EnergyReport
            {
                KilowattHours = kWh,
                CarbonKg = kWh * 0.3, // Assuming West Europe region
                ResourceType = "Infrastructure",
                UtilizationPercentage = 50
            };

            var blobName = await _storageService.SaveReportToBlob(report);
            await _storageService.SaveReportToTable(report);

            return Ok(new { ReportId = blobName, kWh });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing infrastructure");
            return StatusCode(500, "Error analyzing infrastructure");
        }
    }

    [HttpGet("reports/history")]
    public async Task<IActionResult> GetReportHistory([FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate)
    {
        try
        {
            var start = startDate ?? DateTime.UtcNow.AddDays(-30);
            var end = endDate ?? DateTime.UtcNow;

            var reports = await _storageService.GetReportHistory(start, end);
            return Ok(reports);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching report history");
            return StatusCode(500, "Error fetching report history");
        }
    }

    private Task<double> CalculateActualUtilization(List<MicroserviceResourceGroup> microservices, DateTime startTime, DateTime endTime)
    {
        // Optimized calculation for hackathon demo performance
        if (!microservices.Any())
            return Task.FromResult(50.0); // Default baseline
            
        try
        {
            // Fast calculation using LINQ aggregation instead of loops
            var baseUtilization = 50.0;
            var appServiceBonus = microservices.Count(ms => !string.IsNullOrEmpty(ms.AppServiceResourceId)) * 15;
            var functionBonus = microservices.Sum(ms => ms.FunctionAppResourceIds.Count) * 5;
            var serviceBusBonus = microservices.Sum(ms => ms.ServiceBusResourceIds.Count) * 8;
            var databaseBonus = microservices.Sum(ms => ms.DatabaseResourceIds.Count) * 12;
            
            var totalResourceCount = microservices.Count + 
                                   microservices.Sum(ms => ms.FunctionAppResourceIds.Count + 
                                                          ms.ServiceBusResourceIds.Count + 
                                                          ms.DatabaseResourceIds.Count);
            
            var totalUtilization = baseUtilization + 
                                 (appServiceBonus + functionBonus + serviceBusBonus + databaseBonus) / Math.Max(totalResourceCount, 1);
            
            // Quick time-based variance (no complex calculations)
            var timeVariance = (DateTime.Now.Hour >= 9 && DateTime.Now.Hour <= 17) ? 1.1 : 0.9;
            var finalUtilization = Math.Min(Math.Max(totalUtilization * timeVariance, 20), 85);
            
            return Task.FromResult(Math.Round(finalUtilization, 1));
        }
        catch
        {
            return Task.FromResult(50.0); // Fast fallback
        }
    }

    private object GenerateOptimizationRecommendations(EnergyReport energyReport, double currentUtilization)
    {
        var recommendations = new List<object>();
        var totalEnergy = energyReport.KilowattHours;

        // Calculate potential savings
        var potentialSavings = new
        {
            ScaleDownRecommendation = currentUtilization < 50 ? 
                new {
                    Action = "Scale Down Resources",
                    Description = $"Current utilization is {currentUtilization}%. Consider scaling down to save energy.",
                    PotentialSavings = $"{(totalEnergy * 0.3):F2} kWh ({(totalEnergy * 0.3 / totalEnergy * 100):F1}%)",
                    CarbonReduction = $"{(totalEnergy * 0.3 * 0.24):F2} kg CO₂"
                } : null,
            
            RegionOptimization = new {
                Action = "Region Migration to Low-Carbon Areas",
                Description = "Move to regions with renewable energy sources",
                CurrentGrid = "West Europe (0.24 kg CO₂/kWh)",
                OptimalGrid = "France (0.06 kg CO₂/kWh) or Norway (0.02 kg CO₂/kWh)",
                CarbonReduction = $"{(totalEnergy * (0.24 - 0.06)):F2} kg CO₂ (75% reduction)"
            },

            RightSizing = new {
                Action = "Right-size App Service Plans",
                Description = "Optimize App Service Plan tiers based on actual CPU and memory usage patterns",
                PotentialSavings = $"{(totalEnergy * 0.2):F2} kWh",
                CarbonReduction = $"{(totalEnergy * 0.2 * 0.24):F2} kg CO₂",
                Recommendation = "Consider downgrading from Premium to Standard tiers if utilization is below 60%"
            },

            AutoScaling = new {
                Action = "Optimize Function Apps & App Service Scaling",
                Description = "Leverage consumption-based scaling for Function Apps and auto-scaling for App Services",
                PotentialSavings = $"{(totalEnergy * 0.25):F2} kWh during off-peak hours",
                CarbonReduction = $"{(totalEnergy * 0.25 * 0.24):F2} kg CO₂",
                Recommendation = "Use Function Apps for event-driven workloads and enable App Service auto-scaling"
            },

            ServerlessOptimization = new {
                Action = "Maximize Serverless Architecture",
                Description = "Replace always-on App Services with Function Apps for sporadic workloads",
                PotentialSavings = $"{(totalEnergy * 0.15):F2} kWh",
                CarbonReduction = $"{(totalEnergy * 0.15 * 0.24):F2} kg CO₂",
                Recommendation = "Migrate low-frequency APIs to consumption-based Function Apps"
            }
        };

        return new {
            CurrentSituation = new {
                TotalEnergy = $"{totalEnergy:F2} kWh",
                CarbonFootprint = $"{(totalEnergy * 0.24):F2} kg CO₂",
                Utilization = $"{currentUtilization}%",
                Status = currentUtilization > 80 ? "Highly Utilized" : 
                        currentUtilization > 50 ? "Moderately Utilized" : "Under-utilized"
            },
            Recommendations = new List<object> {
                potentialSavings.RegionOptimization,
                potentialSavings.RightSizing,
                potentialSavings.AutoScaling,
                potentialSavings.ServerlessOptimization
            }.Concat(potentialSavings.ScaleDownRecommendation != null ? new[] { potentialSavings.ScaleDownRecommendation } : new object[0]).ToArray(),
            Summary = new {
                MaxPotentialSavings = $"{(totalEnergy * 0.5):F2} kWh",
                MaxCarbonReduction = $"{(totalEnergy * 0.5 * 0.24):F2} kg CO₂",
                RecommendedActions = currentUtilization < 50 ? 3 : 2
            }
        };
    }

    private async Task<IActionResult> AnalyzeAllSubscriptionResources(PlatformEnergyRequest request)
    {
        try
        {
            _logger.LogInformation("Starting direct Azure subscription analysis");
            
            // Get all Azure resources from the subscription using the discovery service
            var discoveryService = HttpContext.RequestServices.GetRequiredService<EnergyCalculator.Services.IAzureResourceDiscoveryService>();
            var allResources = await discoveryService.DiscoverAllResourcesAsync();
            
            var timeSpan = request.EndTime - request.StartTime;
            var daysInRange = Math.Max(1, timeSpan.TotalDays);
            
            // Calculate base utilization for direct resource analysis
            var baseUtilization = 45.0; // Default utilization for subscription-level analysis
            var utilizationFactor = baseUtilization / 100.0;
            
            var energyReport = new EnergyReport
            {
                KilowattHours = 0,
                ResourceType = "Azure Subscription",
                UtilizationPercentage = baseUtilization,
                Details = new Dictionary<string, double>()
            };

            double totalEnergy = 0;

            // Group resources by type for analysis
            var resourceGroups = allResources.GroupBy(r => r.Type).ToDictionary(g => g.Key, g => g.ToList());
            
            // Analyze App Services
            if (resourceGroups.ContainsKey("Microsoft.Web/sites"))
            {
                var appServices = resourceGroups["Microsoft.Web/sites"];
                foreach (var appService in appServices)
                {
                    var energy = CalculateAppServiceEnergyFromResourceId(appService.Id, daysInRange, utilizationFactor);
                    energyReport.Details[$"AppService_{appService.Name}"] = Math.Round(energy, 2);
                    totalEnergy += energy;
                }
            }
            
            // Analyze Storage Accounts
            if (resourceGroups.ContainsKey("Microsoft.Storage/storageAccounts"))
            {
                var storageAccounts = resourceGroups["Microsoft.Storage/storageAccounts"];
                foreach (var storage in storageAccounts)
                {
                    var energy = CalculateStorageAccountEnergy(storage.Id, daysInRange, utilizationFactor);
                    energyReport.Details[$"Storage_{storage.Name}"] = Math.Round(energy, 2);
                    totalEnergy += energy;
                }
            }
            
            // Analyze Service Bus
            if (resourceGroups.ContainsKey("Microsoft.ServiceBus/namespaces"))
            {
                var serviceBusNamespaces = resourceGroups["Microsoft.ServiceBus/namespaces"];
                foreach (var sb in serviceBusNamespaces)
                {
                    var energy = CalculateServiceBusEnergyFromResourceId(sb.Id, daysInRange, utilizationFactor);
                    energyReport.Details[$"ServiceBus_{sb.Name}"] = Math.Round(energy, 2);
                    totalEnergy += energy;
                }
            }
            
            // Analyze Key Vaults (low energy consumption)
            if (resourceGroups.ContainsKey("Microsoft.KeyVault/vaults"))
            {
                var keyVaults = resourceGroups["Microsoft.KeyVault/vaults"];
                var totalKvEnergy = keyVaults.Count * 0.01 * daysInRange; // Very low energy
                energyReport.Details["KeyVaults_Total"] = Math.Round(totalKvEnergy, 2);
                totalEnergy += totalKvEnergy;
            }

            // Set totals
            energyReport.KilowattHours = Math.Round(totalEnergy, 2);
            energyReport.CarbonKg = Math.Round(totalEnergy * 0.24, 2); // EU grid factor
            energyReport.SyncDetailsToJson();

            // Generate trends for subscription analysis
            var trends = GenerateSubscriptionTrends(request, totalEnergy);
            
            // Generate optimization recommendations for subscription
            var uniqueResourceTypes = allResources.GroupBy(r => r.Type).Count();
            var recommendations = GenerateSubscriptionOptimizationRecommendations(totalEnergy, uniqueResourceTypes);

            var reportId = $"subscription_analysis_{DateTime.UtcNow:yyyyMMdd_HHmmss}";
            
            var result = new
            {
                ReportId = reportId,
                EnergyReport = energyReport,
                Trends = trends,
                OptimizationRecommendations = recommendations,
                PerformanceMetrics = new
                {
                    ProcessingTimeMs = 150, // Estimated for subscription analysis
                    ResourceTypesAnalyzed = uniqueResourceTypes,
                    TotalResourcesAnalyzed = allResources.Count
                },
                Note = "Direct Azure subscription resource analysis - all discoverable resources included"
            };

            _logger.LogInformation("Subscription analysis completed: {Energy} kWh across {Types} resource types", 
                totalEnergy, uniqueResourceTypes);
            
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing subscription resources");
            return StatusCode(500, new { error = "Failed to analyze subscription resources", details = ex.Message });
        }
    }

    private double CalculateAppServiceEnergyFromResourceId(string resourceId, double days, double utilizationFactor)
    {
        // Standard App Service energy calculation
        return 0.1 * days * utilizationFactor;
    }

    private double CalculateStorageAccountEnergy(string resourceId, double days, double utilizationFactor)
    {
        // Storage accounts have lower energy consumption
        return 0.02 * days * utilizationFactor;
    }

    private double CalculateServiceBusEnergyFromResourceId(string resourceId, double days, double utilizationFactor)
    {
        // Service Bus energy calculation
        return 0.05 * days * utilizationFactor;
    }

    private object GenerateSubscriptionTrends(PlatformEnergyRequest request, double totalEnergy)
    {
        // Generate sample trends for subscription analysis
        var timeSpan = request.EndTime - request.StartTime;
        var days = Math.Max(1, (int)timeSpan.TotalDays);
        
        var dailyTrends = new List<object>();
        var baseEnergy = totalEnergy / days;
        
        for (int i = 0; i < Math.Min(days, 7); i++)
        {
            var date = request.StartTime.Date.AddDays(i);
            var variance = (new Random(date.Day).NextDouble() - 0.5) * 0.2; // ±10% variance
            var dayEnergy = baseEnergy * (1 + variance);
            
            dailyTrends.Add(new
            {
                Date = date.ToString("yyyy-MM-dd"),
                TotalEnergy = Math.Round(dayEnergy, 2),
                Subscription = new { AzureResources = Math.Round(dayEnergy, 2) }
            });
        }
        
        return new
        {
            Daily = dailyTrends,
            Weekly = new List<object>(),
            Monthly = new List<object>()
        };
    }

    private object GenerateSubscriptionOptimizationRecommendations(double totalEnergy, int resourceTypeCount)
    {
        return new
        {
            CurrentSituation = new
            {
                TotalEnergy = $"{totalEnergy:F2} kWh",
                CarbonFootprint = $"{(totalEnergy * 0.24):F2} kg CO₂",
                ResourceTypes = resourceTypeCount,
                Status = "Subscription-level Analysis"
            },
            Recommendations = new[]
            {
                new
                {
                    Action = "Resource Consolidation",
                    Description = "Consolidate similar resources to reduce overhead and improve efficiency",
                    PotentialSavings = $"{(totalEnergy * 0.15):F2} kWh",
                    CarbonReduction = $"{(totalEnergy * 0.15 * 0.24):F2} kg CO₂",
                    Recommendation = "Review resource groups for consolidation opportunities"
                },
                new
                {
                    Action = "Unused Resource Cleanup",
                    Description = "Identify and remove unused or underutilized resources",
                    PotentialSavings = $"{(totalEnergy * 0.20):F2} kWh",
                    CarbonReduction = $"{(totalEnergy * 0.20 * 0.24):F2} kg CO₂",
                    Recommendation = "Implement automated resource tagging and cleanup policies"
                },
                new
                {
                    Action = "Right-sizing Analysis",
                    Description = "Optimize resource sizes based on actual usage patterns",
                    PotentialSavings = $"{(totalEnergy * 0.25):F2} kWh",
                    CarbonReduction = $"{(totalEnergy * 0.25 * 0.24):F2} kg CO₂",
                    Recommendation = "Use Azure Advisor recommendations for resource optimization"
                }
            },
            Summary = new
            {
                MaxPotentialSavings = $"{(totalEnergy * 0.6):F2} kWh",
                MaxCarbonReduction = $"{(totalEnergy * 0.6 * 0.24):F2} kg CO₂",
                RecommendedActions = 3
            }
        };
    }
}
