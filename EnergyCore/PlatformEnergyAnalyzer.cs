using Azure.Monitor.Query;
using Azure.Identity;
using System.Collections.Concurrent;

namespace EnergyCore
{
    public class PlatformEnergyAnalyzer
    {
        private readonly AzureResourceAnalyzer _resourceAnalyzer;
        private readonly MetricsQueryClient _metricsClient;

        public PlatformEnergyAnalyzer(string subscriptionId)
        {
            _resourceAnalyzer = new AzureResourceAnalyzer(subscriptionId);
            _metricsClient = new MetricsQueryClient(new DefaultAzureCredential());
        }

        public async Task<PlatformEnergyReport> AnalyzePlatformEnergy(
            PlatformResources platform,
            DateTime startTime,
            DateTime endTime)
        {
            var report = new PlatformEnergyReport
            {
                PlatformName = platform.PlatformName,
                StartTime = startTime,
                EndTime = endTime
            };

            // Analyze each microservice in parallel
            var microserviceTasks = platform.Microservices.Select(async microservice =>
            {
                var microserviceReport = await AnalyzeMicroserviceEnergy(microservice, startTime, endTime);
                return (microservice.MicroserviceName, microserviceReport);
            });

            var microserviceResults = await Task.WhenAll(microserviceTasks);
            foreach (var (name, microserviceReport) in microserviceResults)
            {
                report.MicroserviceReports[name] = microserviceReport;
            }

            // Analyze shared resources
            if (platform.SharedResourceIds.Any())
            {
                report.SharedResourcesSummary = await AnalyzeSharedResources(platform.SharedResourceIds, startTime, endTime);
            }

            // Calculate platform total
            report.TotalEnergyConsumption = report.MicroserviceReports.Values.Sum(m => m.TotalEnergyConsumption)
                + (report.SharedResourcesSummary?.TotalEnergy ?? 0);

            return report;
        }

        private async Task<MicroserviceEnergyReport> AnalyzeMicroserviceEnergy(
            MicroserviceResources microservice,
            DateTime startTime,
            DateTime endTime)
        {
            var report = new MicroserviceEnergyReport
            {
                MicroserviceName = microservice.MicroserviceName
            };

            var tasks = new List<Task>();

            // Analyze App Service
            if (!string.IsNullOrEmpty(microservice.AppServiceResourceId))
            {
                tasks.Add(AnalyzeAppServiceEnergy(microservice.AppServiceResourceId, startTime, endTime)
                    .ContinueWith(t =>
                    {
                        var (energy, metrics) = t.Result;
                        report.ResourceBreakdown.AppServiceEnergy = energy;
                        report.DetailedMetrics.Add(metrics);
                    }));
            }

            // Analyze Function Apps
            foreach (var functionAppId in microservice.FunctionAppResourceIds)
            {
                tasks.Add(_resourceAnalyzer.CalculateFunctionAppEnergyUsage(functionAppId, startTime, endTime)
                    .ContinueWith(t =>
                    {
                        var energy = t.Result;
                        report.ResourceBreakdown.FunctionAppsEnergy += energy;
                        report.DetailedMetrics.Add(new MicroserviceEnergyReport.ResourceMetrics
                        {
                            ResourceId = functionAppId,
                            ResourceType = "FunctionApp",
                            ResourceName = ExtractResourceName(functionAppId),
                            EnergyConsumption = energy
                        });
                    }));
            }

            // Analyze Service Bus
            foreach (var serviceBusId in microservice.ServiceBusResourceIds)
            {
                tasks.Add(_resourceAnalyzer.CalculateServiceBusEnergyUsage(serviceBusId, startTime, endTime)
                    .ContinueWith(t =>
                    {
                        var energy = t.Result;
                        report.ResourceBreakdown.ServiceBusEnergy += energy;
                        report.DetailedMetrics.Add(new MicroserviceEnergyReport.ResourceMetrics
                        {
                            ResourceId = serviceBusId,
                            ResourceType = "ServiceBus",
                            ResourceName = ExtractResourceName(serviceBusId),
                            EnergyConsumption = energy
                        });
                    }));
            }

            // Analyze Databases
            foreach (var dbId in microservice.DatabaseResourceIds)
            {
                tasks.Add(_resourceAnalyzer.EstimateMongoDbEnergyUsage(dbId, startTime, endTime)
                    .ContinueWith(t =>
                    {
                        var energy = t.Result;
                        report.ResourceBreakdown.DatabaseEnergy += energy;
                        report.DetailedMetrics.Add(new MicroserviceEnergyReport.ResourceMetrics
                        {
                            ResourceId = dbId,
                            ResourceType = "Database",
                            ResourceName = ExtractResourceName(dbId),
                            EnergyConsumption = energy
                        });
                    }));
            }

            await Task.WhenAll(tasks);

            report.TotalEnergyConsumption = 
                report.ResourceBreakdown.AppServiceEnergy +
                report.ResourceBreakdown.FunctionAppsEnergy +
                report.ResourceBreakdown.ServiceBusEnergy +
                report.ResourceBreakdown.DatabaseEnergy;

            return report;
        }

        private async Task<(double Energy, MicroserviceEnergyReport.ResourceMetrics Metrics)> AnalyzeAppServiceEnergy(
            string resourceId,
            DateTime startTime,
            DateTime endTime)
        {
            var metrics = await _metricsClient.QueryResourceAsync(
                resourceId,
                new[] { "CpuPercentage", "MemoryPercentage", "Http2xx", "Http4xx", "Http5xx" },
                new MetricsQueryOptions
                {
                    TimeRange = new QueryTimeRange(startTime, endTime),
                    Granularity = TimeSpan.FromHours(1)
                });

            var detailedMetrics = new Dictionary<string, double>();
            double totalEnergy = 0;
            
            foreach (var metric in metrics.Value.Metrics)
            {
                var average = metric.TimeSeries.First().Values.Average(v => v.Average ?? 0);
                detailedMetrics[metric.Name] = average;

                // Energy calculation factors
                switch (metric.Name)
                {
                    case "CpuPercentage":
                        totalEnergy += average * 0.02; // 0.02 kWh per CPU percentage point per hour
                        break;
                    case "MemoryPercentage":
                        totalEnergy += average * 0.01; // 0.01 kWh per Memory percentage point per hour
                        break;
                    default:
                        // Add small energy cost for HTTP requests
                        if (metric.Name.StartsWith("Http"))
                        {
                            totalEnergy += average * 0.0001; // 0.0001 kWh per request
                        }
                        break;
                }
            }

            return (totalEnergy, new MicroserviceEnergyReport.ResourceMetrics
            {
                ResourceId = resourceId,
                ResourceType = "AppService",
                ResourceName = ExtractResourceName(resourceId),
                EnergyConsumption = totalEnergy,
                DetailedMetrics = detailedMetrics
            });
        }

        private async Task<ResourceTypeEnergySummary> AnalyzeSharedResources(
            List<string> resourceIds,
            DateTime startTime,
            DateTime endTime)
        {
            var summary = new ResourceTypeEnergySummary();
            var tasks = new ConcurrentBag<Task>();

            foreach (var resourceId in resourceIds)
            {
                if (resourceId.Contains("/Microsoft.Web/sites/"))
                {
                    if (resourceId.Contains("/functions/"))
                    {
                        tasks.Add(_resourceAnalyzer.CalculateFunctionAppEnergyUsage(resourceId, startTime, endTime)
                            .ContinueWith(t => summary.FunctionAppsEnergy += t.Result));
                    }
                    else
                    {
                        tasks.Add(AnalyzeAppServiceEnergy(resourceId, startTime, endTime)
                            .ContinueWith(t => summary.AppServiceEnergy += t.Result.Energy));
                    }
                }
                else if (resourceId.Contains("/Microsoft.ServiceBus/"))
                {
                    tasks.Add(_resourceAnalyzer.CalculateServiceBusEnergyUsage(resourceId, startTime, endTime)
                        .ContinueWith(t => summary.ServiceBusEnergy += t.Result));
                }
                else if (resourceId.Contains("/Microsoft.DocumentDB/"))
                {
                    tasks.Add(_resourceAnalyzer.EstimateMongoDbEnergyUsage(resourceId, startTime, endTime)
                        .ContinueWith(t => summary.DatabaseEnergy += t.Result));
                }
            }

            await Task.WhenAll(tasks);
            return summary;
        }

        private string ExtractResourceName(string resourceId)
        {
            return resourceId.Split('/').Last();
        }
    }
}
