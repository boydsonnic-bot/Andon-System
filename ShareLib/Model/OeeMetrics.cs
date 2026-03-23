namespace SharedLib.Model
{
    public class OeeMetrics
    {
        public string StationName { get; set; } = "";
        public int OeeValue { get; set; }
        public int Availability { get; set; }
        public int Performance { get; set; }
        public int Quality { get; set; }
    }
}