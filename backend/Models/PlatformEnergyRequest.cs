namespace EnergyCalculator.Models
{
    public class PlatformEnergyRequest
    {
        public List<MicroserviceResourceGroup> Microservices { get; set; } = new();
        public List<string> SharedResourceIds { get; set; } = new();
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public bool AnalyzeAllResources { get; set; } = false; // Flag for direct subscription analysis
        // Removed manual utilization - will calculate from actual Azure metrics
    }

    public class MicroserviceResourceGroup
    {
        public required string MicroserviceName { get; set; }
        public required string AppServiceResourceId { get; set; } // Required for microservice classification
        public List<string> FunctionAppResourceIds { get; set; } = new();
        public List<string> ServiceBusResourceIds { get; set; } = new();
        public List<string> DatabaseResourceIds { get; set; } = new();
    }
}
