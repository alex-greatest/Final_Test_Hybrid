using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Common.Settings;
using Final_Test_Hybrid.Services.Database;
using Final_Test_Hybrid.Services.Main;
using Final_Test_Hybrid.Services.SpringBoot.Operator;
using Final_Test_Hybrid.Services.SpringBoot.Shift;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.PreExecution;

namespace Final_Test_Hybrid.Services.Steps.Steps;

public class InitializeDatabaseStep(
    AppSettingsService appSettings,
    BoilerService boilerService,
    OperationService operationService,
    OperatorState operatorState,
    BoilerState boilerState,
    ShiftState shiftState,
    ExecutionMessageState messageState,
    DualLogger<InitializeDatabaseStep> logger) : IPreExecutionStep
{
    private const string UnknownOperator = "Unknown";

    public string Id => "initialize-database";
    public string Name => "Инициализация БД";
    public string Description => "Создаёт записи котла и операции в локальной БД";
    public bool IsVisibleInStatusGrid => false;

    public async Task<PreExecutionResult> ExecuteAsync(PreExecutionContext context, CancellationToken ct)
    {
        if (appSettings.UseMes)
        {
            logger.LogInformation("MES режим: пропуск создания записей в локальной БД");
            return PreExecutionResult.Continue();
        }
        if (string.IsNullOrEmpty(boilerState.SerialNumber) || boilerState.BoilerTypeCycle == null)
        {
            return PreExecutionResult.Fail("Данные котла не инициализированы", "Ошибка инициализации");
        }
        messageState.SetMessage("Создание записей в БД...");
        var operatorName = operatorState.Username ?? UnknownOperator;
        var boiler = await boilerService.FindOrCreateAsync(
            boilerState.SerialNumber,
            boilerState.BoilerTypeCycle.Id,
            operatorName);
        await operationService.CreateAsync(boiler.Id, operatorName, shiftState.ShiftNumber ?? 0);
        logger.LogInformation("Записи в БД созданы: Boiler={BoilerId}", boiler.Id);
        return PreExecutionResult.Continue();
    }
}
