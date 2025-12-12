using System.Reflection;

namespace Final_Test_Hybrid.Services.Steps;

public class TestStepRegistry : ITestStepRegistry
{
    public IReadOnlyList<ITestStep> Steps { get; } = LoadSteps();

    public ITestStep? GetById(string id)
    {
        return Steps.FirstOrDefault(s => s.Id == id);
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
        return typeof(ITestStep).IsAssignableFrom(type) && !type.IsInterface && !type.IsAbstract;
    }

    private static ITestStep? CreateInstance(Type type)
    {
        return Activator.CreateInstance(type) as ITestStep;
    }
}
