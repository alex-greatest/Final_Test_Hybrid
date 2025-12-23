using System.Reflection;
using Final_Test_Hybrid.Services.Steps.Interaces;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure;

public class TestStepRegistry : ITestStepRegistry
{
    public IReadOnlyList<ITestStep> Steps { get; } = LoadSteps();
    public IReadOnlyList<ITestStep> VisibleSteps => Steps.Where(s => s.IsVisibleInEditor).ToList();

    public ITestStep? GetById(string id)
    {
        return Steps.FirstOrDefault(s => s.Id == id);
    }

    public ITestStep? GetByName(string name)
    {
        return Steps.FirstOrDefault(s => s.Name == name);
    }

    private static List<ITestStep> LoadSteps()
    {
        return Assembly.GetExecutingAssembly()
            .GetTypes()
            .Where(IsTestStepType)
            .Select(CreateInstance)
            .Where(s => s != null)
            .Cast<ITestStep>()
            .OrderBy(s => s.Name)
            .ToList();
    }

    private static bool IsTestStepType(Type type)
    {
        return typeof(ITestStep).IsAssignableFrom(type) && type is { IsInterface: false, IsAbstract: false };
    }

    private static ITestStep? CreateInstance(Type type)
    {
        return Activator.CreateInstance(type) as ITestStep;
    }
}
