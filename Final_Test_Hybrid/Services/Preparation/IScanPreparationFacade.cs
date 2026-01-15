using Final_Test_Hybrid.Models;
using Final_Test_Hybrid.Services.Main;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.PreExecution;

namespace Final_Test_Hybrid.Services.Preparation;

public interface IScanPreparationFacade
{
    Task<PreExecutionResult?> LoadBoilerDataAsync(PreExecutionContext context);

    Task<PreExecutionResult?> InitializeDatabaseAsync(
        BoilerState boilerState,
        string operatorName,
        int shiftNumber);
}
