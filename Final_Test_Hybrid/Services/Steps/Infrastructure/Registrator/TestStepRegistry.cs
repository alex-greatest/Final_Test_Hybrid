using System.Reflection;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Test;
using Microsoft.Extensions.DependencyInjection;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Registrator;

public class TestStepRegistry : ITestStepRegistry
{
    private readonly IServiceProvider _serviceProvider;
    private List<ITestStep>? _steps;
    public IReadOnlyList<ITestStep> Steps => _steps ??= LoadSteps();
    public IReadOnlyList<ITestStep> VisibleSteps => Steps.Where(s => s.IsVisibleInEditor).ToList();

    public TestStepRegistry(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public ITestStep? GetById(string id)
    {
        return Steps.FirstOrDefault(s => s.Id == id);
    }

    public ITestStep? GetByName(string name)
    {
        return Steps.FirstOrDefault(s => s.Name == name);
    }

    private List<ITestStep> LoadSteps()
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

    private ITestStep? CreateInstance(Type type)
    {
        return ActivatorUtilities.CreateInstance(_serviceProvider, type) as ITestStep;
    }
}
