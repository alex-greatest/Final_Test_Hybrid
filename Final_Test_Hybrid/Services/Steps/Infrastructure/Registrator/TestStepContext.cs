using Final_Test_Hybrid.Services.OpcUa;
using Microsoft.Extensions.Logging;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Registrator;

public class TestStepContext(int columnIndex, OpcUaTagService opcUa, ILogger logger)
{
    public int ColumnIndex { get; } = columnIndex;
    public OpcUaTagService OpcUa { get; } = opcUa;
    public ILogger Logger { get; } = logger;
    public Dictionary<string, object> Variables { get; } = [];
}
