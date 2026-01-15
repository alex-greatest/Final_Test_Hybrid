using Final_Test_Hybrid.Models;
using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.PreExecution;

namespace Final_Test_Hybrid.Services.Preparation;

public class ScanPreparationFacade(
    IBoilerDataLoader dataLoader,
    IBoilerDatabaseInitializer dbInitializer,
    DualLogger<ScanPreparationFacade> logger) : IScanPreparationFacade
{
    public async Task<PreExecutionResult?> LoadBoilerDataAsync(PreExecutionContext context)
    {
        var article = context.BarcodeValidation!.Article!;
        var result = await dataLoader.LoadAsync(article);

        if (!result.IsSuccess)
        {
            return PreExecutionResult.Fail(result.Error!);
        }

        context.BoilerTypeCycle = result.BoilerTypeCycle;
        context.Recipes = result.Recipes;

        logger.LogInformation("Данные котла загружены: {Type}, рецептов: {Count}",
            result.BoilerTypeCycle!.Type, result.Recipes!.Count);

        return null;
    }

    public async Task<PreExecutionResult?> InitializeDatabaseAsync(
        BoilerState boilerState,
        string operatorName,
        int shiftNumber)
    {
        if (string.IsNullOrEmpty(boilerState.SerialNumber) || boilerState.BoilerTypeCycle == null)
        {
            return PreExecutionResult.Fail("Данные котла не инициализированы", "Ошибка инициализации");
        }

        var result = await dbInitializer.InitializeAsync(
            boilerState.SerialNumber,
            boilerState.BoilerTypeCycle.Id,
            operatorName,
            shiftNumber);

        if (!result.IsSuccess)
        {
            return PreExecutionResult.Fail(result.Error!);
        }

        return null;
    }
}
