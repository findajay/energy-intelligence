namespace EnergyCore
{
    public class ResourceTypeEnergySummary
    {
        public double AppServiceEnergy { get; set; }
        public double FunctionAppsEnergy { get; set; }
        public double ServiceBusEnergy { get; set; }
        public double DatabaseEnergy { get; set; }
        public double TotalEnergy => AppServiceEnergy + FunctionAppsEnergy + ServiceBusEnergy + DatabaseEnergy;
    }

    public class MicroserviceResources
    {
        public string MicroserviceName { get; set; }
        public string AppServiceResourceId { get; set; }
        public List<string> FunctionAppResourceIds { get; set; } = new();
        public List<string> ServiceBusResourceIds { get; set; } = new();
        public List<string> DatabaseResourceIds { get; set; } = new();
    }

    public class PlatformResources
    {
        public string PlatformName { get; set; }
        public List<MicroserviceResources> Microservices { get; set; } = new();
        public List<string> SharedResourceIds { get; set; } = new(); // For shared resources like API Management, shared Service Bus, etc.
    }

    public class EnergyTrend
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    public class ResourceTrends
    {
        public string ResourceId { get; set; }
        public string ResourceName { get; set; }
        public string ResourceType { get; set; }
        public List<EnergyTrend> DailyTrends { get; set; } = new();
        public List<EnergyTrend> WeeklyTrends { get; set; } = new();
        public List<EnergyTrend> MonthlyTrends { get; set; } = new();
    }

    public class MicroserviceTrends
    {
        public string MicroserviceName { get; set; }
        public List<EnergyTrend> DailyTrends { get; set; } = new();
        public List<EnergyTrend> WeeklyTrends { get; set; } = new();
        public List<EnergyTrend> MonthlyTrends { get; set; } = new();
        public Dictionary<string, ResourceTrends> ResourceTrends { get; set; } = new();
    }

    public class PlatformTrends
    {
        public string PlatformName { get; set; }
        public List<EnergyTrend> DailyTrends { get; set; } = new();
        public List<EnergyTrend> WeeklyTrends { get; set; } = new();
        public List<EnergyTrend> MonthlyTrends { get; set; } = new();
        public Dictionary<string, MicroserviceTrends> MicroserviceTrends { get; set; } = new();
        public ResourceTypeEnergySummary AverageDailyConsumption { get; set; }
    }

    public class PlatformEnergyReport
    {
        public string PlatformName { get; set; }
        public double TotalEnergyConsumption { get; set; }
        public Dictionary<string, MicroserviceEnergyReport> MicroserviceReports { get; set; } = new();
        public ResourceTypeEnergySummary SharedResourcesSummary { get; set; }
        public PlatformTrends Trends { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }


    }

    public class MicroserviceEnergyReport
    {
        public string MicroserviceName { get; set; }
        public double TotalEnergyConsumption { get; set; }
        public ResourceTypeEnergySummary ResourceBreakdown { get; set; } = new();
        public List<ResourceMetrics> DetailedMetrics { get; set; } = new();

        public class ResourceMetrics
        {
            public string ResourceId { get; set; }
            public string ResourceType { get; set; }
            public string ResourceName { get; set; }
            public double EnergyConsumption { get; set; }
            public Dictionary<string, double> DetailedMetrics { get; set; } = new();
        }
    }
}
