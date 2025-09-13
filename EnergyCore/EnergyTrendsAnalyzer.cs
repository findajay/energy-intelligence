using Azure.Data.Tables;
using System.Collections.Concurrent;
using System.Globalization;

namespace EnergyCore
{
    public class EnergyTrendsAnalyzer
    {
        private readonly TableClient _tableClient;
        private readonly PlatformEnergyAnalyzer _energyAnalyzer;

        public EnergyTrendsAnalyzer(string connectionString, string subscriptionId)
        {
            _tableClient = new TableClient(connectionString, "EnergyReports");
            _energyAnalyzer = new PlatformEnergyAnalyzer(subscriptionId);
        }

        public async Task<PlatformTrends> AnalyzeTrends(PlatformResources platform, DateTime endDate)
        {
            var trends = new PlatformTrends
            {
                PlatformName = platform.PlatformName,
                DailyTrends = new List<EnergyTrend>(),
                WeeklyTrends = new List<EnergyTrend>(),
                MonthlyTrends = new List<EnergyTrend>()
            };

            // Calculate daily trends for the last 30 days
            var dailyTrends = await CalculateDailyTrends(platform, endDate.AddDays(-30), endDate);
            trends.DailyTrends.AddRange(dailyTrends);

            // Calculate weekly trends for the last 12 weeks
            var weeklyTrends = await CalculateWeeklyTrends(platform, endDate.AddDays(-84), endDate);
            trends.WeeklyTrends.AddRange(weeklyTrends);

            // Calculate monthly trends for the last 12 months
            var monthlyTrends = await CalculateMonthlyTrends(platform, endDate.AddMonths(-12), endDate);
            trends.MonthlyTrends.AddRange(monthlyTrends);

            // Calculate microservice-specific trends
            foreach (var microservice in platform.Microservices)
            {
                var microserviceTrends = await AnalyzeMicroserviceTrends(microservice, endDate);
                trends.MicroserviceTrends[microservice.MicroserviceName] = microserviceTrends;
            }

            // Calculate average daily consumption
            trends.AverageDailyConsumption = CalculateAverageDailyConsumption(dailyTrends);

            return trends;
        }

        private async Task<List<EnergyTrend>> CalculateDailyTrends(
            PlatformResources platform,
            DateTime startDate,
            DateTime endDate)
        {
            var trends = new List<EnergyTrend>();
            var currentDate = startDate;

            while (currentDate <= endDate)
            {
                var nextDate = currentDate.AddDays(1);
                var report = await _energyAnalyzer.AnalyzePlatformEnergy(platform, currentDate, nextDate);

                trends.Add(new EnergyTrend
                {
                    Timestamp = currentDate,
                    Value = report.TotalEnergyConsumption
                });

                currentDate = nextDate;
            }

            return trends;
        }

        private async Task<List<EnergyTrend>> CalculateWeeklyTrends(
            PlatformResources platform,
            DateTime startDate,
            DateTime endDate)
        {
            var trends = new List<EnergyTrend>();
            var currentDate = startDate;

            while (currentDate <= endDate)
            {
                var nextDate = currentDate.AddDays(7);
                var report = await _energyAnalyzer.AnalyzePlatformEnergy(platform, currentDate, nextDate);

                trends.Add(new EnergyTrend
                {
                    Timestamp = currentDate,
                    Value = report.TotalEnergyConsumption / 7 // Average daily consumption for the week
                });

                currentDate = nextDate;
            }

            return trends;
        }

        private async Task<List<EnergyTrend>> CalculateMonthlyTrends(
            PlatformResources platform,
            DateTime startDate,
            DateTime endDate)
        {
            var trends = new List<EnergyTrend>();
            var currentDate = startDate;

            while (currentDate <= endDate)
            {
                var nextDate = currentDate.AddMonths(1);
                var report = await _energyAnalyzer.AnalyzePlatformEnergy(platform, currentDate, nextDate);

                trends.Add(new EnergyTrend
                {
                    Timestamp = currentDate,
                    Value = report.TotalEnergyConsumption / 30 // Average daily consumption for the month
                });

                currentDate = nextDate;
            }

            return trends;
        }

        private async Task<MicroserviceTrends> AnalyzeMicroserviceTrends(
            MicroserviceResources microservice,
            DateTime endDate)
        {
            var trends = new MicroserviceTrends
            {
                MicroserviceName = microservice.MicroserviceName
            };

            // Analyze each resource individually
            var resourceTasks = new List<Task>();

            if (!string.IsNullOrEmpty(microservice.AppServiceResourceId))
            {
                resourceTasks.Add(AnalyzeResourceTrends(
                    microservice.AppServiceResourceId,
                    "AppService",
                    endDate).ContinueWith(t => trends.ResourceTrends[microservice.AppServiceResourceId] = t.Result));
            }

            foreach (var functionApp in microservice.FunctionAppResourceIds)
            {
                resourceTasks.Add(AnalyzeResourceTrends(
                    functionApp,
                    "FunctionApp",
                    endDate).ContinueWith(t => trends.ResourceTrends[functionApp] = t.Result));
            }

            foreach (var serviceBus in microservice.ServiceBusResourceIds)
            {
                resourceTasks.Add(AnalyzeResourceTrends(
                    serviceBus,
                    "ServiceBus",
                    endDate).ContinueWith(t => trends.ResourceTrends[serviceBus] = t.Result));
            }

            foreach (var db in microservice.DatabaseResourceIds)
            {
                resourceTasks.Add(AnalyzeResourceTrends(
                    db,
                    "Database",
                    endDate).ContinueWith(t => trends.ResourceTrends[db] = t.Result));
            }

            await Task.WhenAll(resourceTasks);

            // Calculate aggregated microservice trends
            trends.DailyTrends = AggregateResourceTrends(trends.ResourceTrends.Values.Select(r => r.DailyTrends));
            trends.WeeklyTrends = AggregateResourceTrends(trends.ResourceTrends.Values.Select(r => r.WeeklyTrends));
            trends.MonthlyTrends = AggregateResourceTrends(trends.ResourceTrends.Values.Select(r => r.MonthlyTrends));

            return trends;
        }

        private async Task<ResourceTrends> AnalyzeResourceTrends(
            string resourceId,
            string resourceType,
            DateTime endDate)
        {
            var trends = new ResourceTrends
            {
                ResourceId = resourceId,
                ResourceName = resourceId.Split('/').Last(),
                ResourceType = resourceType
            };

            // Query historical data from Table Storage
            var filter = $"PartitionKey eq '{resourceId}' and Timestamp ge datetime'{endDate.AddDays(-30):yyyy-MM-ddTHH:mm:ssZ}'";
            var dailyData = _tableClient.QueryAsync<TableEntity>(filter);

            var dailyTrends = new ConcurrentBag<EnergyTrend>();
            await foreach (var entity in dailyData)
            {
                dailyTrends.Add(new EnergyTrend
                {
                    Timestamp = entity.Timestamp.Value.DateTime,
                    Value = double.Parse(entity.GetString("EnergyConsumption"))
                });
            }

            trends.DailyTrends = dailyTrends.OrderBy(t => t.Timestamp).ToList();
            trends.WeeklyTrends = AggregateToWeeklyTrends(trends.DailyTrends);
            trends.MonthlyTrends = AggregateToMonthlyTrends(trends.DailyTrends);

            return trends;
        }

        private List<EnergyTrend> AggregateResourceTrends(IEnumerable<List<EnergyTrend>> resourceTrends)
        {
            return resourceTrends
                .SelectMany(t => t)
                .GroupBy(t => t.Timestamp)
                .Select(g => new EnergyTrend
                {
                    Timestamp = g.Key,
                    Value = g.Sum(t => t.Value)
                })
                .OrderBy(t => t.Timestamp)
                .ToList();
        }

        private List<EnergyTrend> AggregateToWeeklyTrends(List<EnergyTrend> dailyTrends)
        {
            return dailyTrends
                .GroupBy(t => ISOWeek.GetWeekOfYear(t.Timestamp))
                .Select(g => new EnergyTrend
                {
                    Timestamp = g.First().Timestamp,
                    Value = g.Average(t => t.Value)
                })
                .OrderBy(t => t.Timestamp)
                .ToList();
        }

        private List<EnergyTrend> AggregateToMonthlyTrends(List<EnergyTrend> dailyTrends)
        {
            return dailyTrends
                .GroupBy(t => new { t.Timestamp.Year, t.Timestamp.Month })
                .Select(g => new EnergyTrend
                {
                    Timestamp = new DateTime(g.Key.Year, g.Key.Month, 1),
                    Value = g.Average(t => t.Value)
                })
                .OrderBy(t => t.Timestamp)
                .ToList();
        }

        private ResourceTypeEnergySummary CalculateAverageDailyConsumption(List<EnergyTrend> dailyTrends)
        {
            return new ResourceTypeEnergySummary
            {
                AppServiceEnergy = dailyTrends.Average(t => t.Value * 0.3), // Estimated proportion
                FunctionAppsEnergy = dailyTrends.Average(t => t.Value * 0.3),
                ServiceBusEnergy = dailyTrends.Average(t => t.Value * 0.2),
                DatabaseEnergy = dailyTrends.Average(t => t.Value * 0.2)
            };
        }
    }
}
