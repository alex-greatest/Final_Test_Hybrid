using System.Reflection;
using Final_Test_Hybrid.Services.Steps.Interaces;
using Microsoft.Extensions.DependencyInjection;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure;

public class TestStepRegistry(IServiceProvider serviceProvider) : ITestStepRegistry
{
    public IReadOnlyList<ITestStep> Steps { get; } = LoadSteps(serviceProvider);
    public IReadOnlyList<ITestStep> VisibleSteps => Steps.Where(s => s.IsVisibleInEditor).ToList();

    public ITestStep? GetById(string id)
    {
        return Steps.FirstOrDefault(s => s.Id == id);
    }

    public ITestStep? GetByName(string name)
    {
        return Steps.FirstOrDefault(s => s.Name == name);
    }

    private static List<ITestStep> LoadSteps(IServiceProvider serviceProvider)
    {
        return Assembly.GetExecutingAssembly()
            .GetTypes()
            .Where(IsTestStepType)
            .Select(type => CreateInstance(serviceProvider, type))
            .Where(s => s != null)
            .Cast<ITestStep>()
            .OrderBy(s => s.Name)
            .ToList();
    }

    private static bool IsTestStepType(Type type)
    {
        return typeof(ITestStep).IsAssignableFrom(type) && type is { IsInterface: false, IsAbstract: false };
    }

    private static ITestStep? CreateInstance(IServiceProvider serviceProvider, Type type)
    {
        return ActivatorUtilities.CreateInstance(serviceProvider, type) as ITestStep;
    }
}
