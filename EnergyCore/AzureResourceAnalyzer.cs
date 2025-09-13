using Azure.Core;
using Azure.Identity;
using Azure.Monitor.Query;
using Azure.Monitor.Query.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace EnergyCore
{
    public class AzureResourceAnalyzer
    {
        private readonly MetricsQueryClient _metricsClient;
        private readonly string _subscriptionId;
        private readonly Dictionary<string, double> _resourceEnergyFactors;

        public AzureResourceAnalyzer(string subscriptionId)
        {
            _metricsClient = new MetricsQueryClient(new DefaultAzureCredential());
            _subscriptionId = subscriptionId;
            
            // Energy consumption factors (kWh per unit) - these are approximate values
            _resourceEnergyFactors = new Dictionary<string, double>
            {
                { "functionExecutions", 0.000017 },  // kWh per function execution
                { "serviceBusMessages", 0.000004 },  // kWh per message
                { "mongoDbOperations", 0.000012 }    // kWh per operation
            };
        }

        public async Task<double> CalculateFunctionAppEnergyUsage(string resourceId, DateTime startTime, DateTime endTime)
        {
            var response = await _metricsClient.QueryResourceAsync(
                resourceId,
                new[] { "FunctionExecutionCount" },
                new MetricsQueryOptions
                {
                    TimeRange = new QueryTimeRange(startTime, endTime),
                    Granularity = TimeSpan.FromHours(1)
                });

            double totalExecutions = 0;
            foreach (var metric in response.Value.Metrics)
            {
                foreach (var timeSeriesElement in metric.TimeSeries)
                {
                    foreach (var metricValue in timeSeriesElement.Values)
                    {
                        totalExecutions += metricValue.Total ?? 0;
                    }
                }
            }

            return totalExecutions * _resourceEnergyFactors["functionExecutions"];
        }

        public async Task<double> CalculateServiceBusEnergyUsage(string resourceId, DateTime startTime, DateTime endTime)
        {
            var response = await _metricsClient.QueryResourceAsync(
                resourceId,
                new[] { "IncomingMessages", "OutgoingMessages" },
                new MetricsQueryOptions
                {
                    TimeRange = new QueryTimeRange(startTime, endTime),
                    Granularity = TimeSpan.FromHours(1)
                });

            double totalMessages = 0;
            foreach (var metric in response.Value.Metrics)
            {
                foreach (var timeSeriesElement in metric.TimeSeries)
                {
                    foreach (var metricValue in timeSeriesElement.Values)
                    {
                        totalMessages += metricValue.Total ?? 0;
                    }
                }
            }

            return totalMessages * _resourceEnergyFactors["serviceBusMessages"];
        }

        public async Task<double> EstimateMongoDbEnergyUsage(string resourceId, DateTime startTime, DateTime endTime)
        {
            // For MongoDB, we'll estimate based on database operations
            // This is an approximation as exact metrics depend on your MongoDB deployment
            var response = await _metricsClient.QueryResourceAsync(
                resourceId,
                new[] { "DatabaseRequests" },
                new MetricsQueryOptions
                {
                    TimeRange = new QueryTimeRange(startTime, endTime),
                    Granularity = TimeSpan.FromHours(1)
                });

            double totalOperations = 0;
            foreach (var metric in response.Value.Metrics)
            {
                foreach (var timeSeriesElement in metric.TimeSeries)
                {
                    foreach (var metricValue in timeSeriesElement.Values)
                    {
                        totalOperations += metricValue.Total ?? 0;
                    }
                }
            }

            return totalOperations * _resourceEnergyFactors["mongoDbOperations"];
        }

        public class ResourceEnergyReport
        {
            public double FunctionAppEnergy { get; set; }
            public double ServiceBusEnergy { get; set; }
            public double MongoDbEnergy { get; set; }
            public double TotalEnergy { get; set; }
            public DateTime StartTime { get; set; }
            public DateTime EndTime { get; set; }
        }

        public async Task<ResourceEnergyReport> GenerateCompleteEnergyReport(
            string functionAppResourceId,
            string serviceBusResourceId,
            string mongoDbResourceId,
            DateTime startTime,
            DateTime endTime)
        {
            var functionAppEnergy = await CalculateFunctionAppEnergyUsage(functionAppResourceId, startTime, endTime);
            var serviceBusEnergy = await CalculateServiceBusEnergyUsage(serviceBusResourceId, startTime, endTime);
            var mongoDbEnergy = await EstimateMongoDbEnergyUsage(mongoDbResourceId, startTime, endTime);

            return new ResourceEnergyReport
            {
                FunctionAppEnergy = functionAppEnergy,
                ServiceBusEnergy = serviceBusEnergy,
                MongoDbEnergy = mongoDbEnergy,
                TotalEnergy = functionAppEnergy + serviceBusEnergy + mongoDbEnergy,
                StartTime = startTime,
                EndTime = endTime
            };
        }
    }
}
