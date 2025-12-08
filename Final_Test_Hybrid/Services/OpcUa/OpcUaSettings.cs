namespace Final_Test_Hybrid.Services.OpcUa
{
    public class OpcUaSettings
    {
        public string EndpointUrl { get; set; } = "opc.tcp://192.168.1.100:4840";
        public string ApplicationName { get; set; } = "FinalTestHybrid";
        public int ReconnectIntervalMs { get; set; } = 5000;
        public int SessionTimeoutMs { get; set; } = 60000;
    }
}


