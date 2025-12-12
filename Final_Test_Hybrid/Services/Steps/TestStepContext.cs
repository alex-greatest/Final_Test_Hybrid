using Final_Test_Hybrid.Services.OpcUa;
using Microsoft.Extensions.Logging;

namespace Final_Test_Hybrid.Services.Steps;

public class TestStepContext(OpcUaTagService opcUa, ILogger logger)
{
    public OpcUaTagService OpcUa { get; } = opcUa;
    public ILogger Logger { get; } = logger;
    public Dictionary<string, object> Variables { get; } = [];
}
