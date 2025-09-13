namespace EnergyCalculator.Models
{
    public class AzureResourceEnergyRequest
    {
        public string FunctionAppResourceId { get; set; }
        public string ServiceBusResourceId { get; set; }
        public string MongoDbResourceId { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
    }
}
