using Final_Test_Hybrid.Models.Database;
using Final_Test_Hybrid.Models.Errors;
using Final_Test_Hybrid.Services.Database.Config;
using Final_Test_Hybrid.Services.Errors;
using Final_Test_Hybrid.Services.Results;
using Final_Test_Hybrid.Services.SpringBoot.Operation.Interrupt;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Timing;
using Final_Test_Hybrid.Services.Storage;
using Final_Test_Hybrid.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace Final_Test_Hybrid.Tests.Runtime;

public class InterruptReasonStorageServiceTests
{
    [Fact]
    public async Task SaveAsync_UpdatesOnlyInWorkOperationAndPersistsSnapshot()
    {
        var databaseName = $"interrupt-db-{Guid.NewGuid():N}";
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;
        var contextFactory = new TestDbContextFactory(options);
        await SeedAsync(contextFactory);

        var testResultsService = new TestResultsService();
        var errorService = new HistoryErrorService(["E1001"]);
        var stepTimingService = new StepTimingService();
        var operationStorage = new OperationStorageService(TestInfrastructure.CreateDualLogger<OperationStorageService>());
        var resultStorage = new ResultStorageService(
            testResultsService,
            TestInfrastructure.CreateDualLogger<ResultStorageService>());
        var errorStorage = new ErrorStorageService(
            errorService,
            TestInfrastructure.CreateDualLogger<ErrorStorageService>());
        var stepTimeStorage = new StepTimeStorageService(
            stepTimingService,
            TestInfrastructure.CreateDualLogger<StepTimeStorageService>());
        var service = new InterruptReasonStorageService(
            contextFactory,
            operationStorage,
            resultStorage,
            errorStorage,
            stepTimeStorage,
            TestInfrastructure.CreateDualLogger<InterruptReasonStorageService>());

        testResultsService.Add("Voltage", "220", "", "", 1, false, "V", "Step A");
        stepTimingService.AddCompletedStepTiming("Step A", "desc", TimeSpan.FromSeconds(5));

        var result = await service.SaveAsync("SN-001", "admin-1", "interrupt reason", CancellationToken.None);

        Assert.True(result.IsSuccess);

        await using var verifyContext = await contextFactory.CreateDbContextAsync(CancellationToken.None);
        var interrupted = await verifyContext.Operations.SingleAsync(item => item.Id == 100, CancellationToken.None);
        var completed = await verifyContext.Operations.SingleAsync(item => item.Id == 101, CancellationToken.None);

        Assert.Equal(OperationResultStatus.Interrupted, interrupted.Status);
        Assert.Equal("interrupt reason", interrupted.Comment);
        Assert.Equal("admin-1", interrupted.AdminInterrupted);
        Assert.NotNull(interrupted.DateEnd);

        Assert.Equal(OperationResultStatus.Ok, completed.Status);
        Assert.Equal("done", completed.Comment);
        Assert.Null(completed.AdminInterrupted);

        Assert.Single(await verifyContext.Results.Where(item => item.OperationId == interrupted.Id).ToListAsync(CancellationToken.None));
        Assert.Single(await verifyContext.Errors.Where(item => item.OperationId == interrupted.Id).ToListAsync(CancellationToken.None));
        Assert.Single(await verifyContext.StepTimes.Where(item => item.OperationId == interrupted.Id).ToListAsync(CancellationToken.None));
        Assert.Empty(await verifyContext.Results.Where(item => item.OperationId == completed.Id).ToListAsync(CancellationToken.None));
    }

    private static async Task SeedAsync(TestDbContextFactory contextFactory)
    {
        await using var context = await contextFactory.CreateDbContextAsync(CancellationToken.None);

        var cycle = new BoilerTypeCycle
        {
            Id = 10,
            BoilerTypeId = 77,
            Type = "Type",
            IsActive = true,
            Article = "ART-1"
        };
        var boiler = new Boiler
        {
            Id = 1,
            SerialNumber = "SN-001",
            BoilerTypeCycleId = cycle.Id,
            BoilerTypeCycle = cycle,
            DateCreate = DateTime.UtcNow,
            Status = OperationResultStatus.InWork,
            Operator = "operator-1"
        };
        var activeOperation = new Operation
        {
            Id = 100,
            BoilerId = boiler.Id,
            Boiler = boiler,
            DateStart = DateTime.UtcNow.AddMinutes(-5),
            Status = OperationResultStatus.InWork,
            NumberShift = 1,
            Version = 1,
            Operator = "operator-1"
        };
        var completedOperation = new Operation
        {
            Id = 101,
            BoilerId = boiler.Id,
            Boiler = boiler,
            DateStart = DateTime.UtcNow.AddMinutes(-30),
            DateEnd = DateTime.UtcNow.AddMinutes(-20),
            Status = OperationResultStatus.Ok,
            NumberShift = 1,
            Comment = "done",
            Version = 1,
            Operator = "operator-1"
        };
        var resultSetting = new ResultSettingHistory
        {
            Id = 200,
            ResultsSettingsId = 1,
            BoilerTypeId = cycle.BoilerTypeId,
            ParameterName = "Voltage",
            AddressValue = "Voltage",
            PlcType = PlcType.REAL,
            AuditType = AuditType.Simple,
            IsActive = true
        };
        var stepHistory = new StepFinalTestHistory
        {
            Id = 300,
            StepFinalTestId = 1,
            Name = "Step A",
            IsActive = true
        };
        var errorHistory = new ErrorSettingsHistory
        {
            Id = 400,
            AddressError = "E1001",
            IsActive = true
        };

        context.BoilerTypeCycles.Add(cycle);
        context.Boilers.Add(boiler);
        context.Operations.AddRange(activeOperation, completedOperation);
        context.ResultSettingHistories.Add(resultSetting);
        context.StepFinalTestHistories.Add(stepHistory);
        context.ErrorSettingsHistories.Add(errorHistory);

        await context.SaveChangesAsync(CancellationToken.None);
    }

    private sealed class TestDbContextFactory(DbContextOptions<AppDbContext> options) : IDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext()
        {
            return new AppDbContext(options);
        }

        public Task<AppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(CreateDbContext());
        }
    }

    private sealed class HistoryErrorService(IReadOnlyList<string> codes) : IErrorService
    {
        public event Action? OnActiveErrorsChanged
        {
            add { }
            remove { }
        }

        public event Action? OnHistoryChanged
        {
            add { }
            remove { }
        }

        public bool HasResettableErrors => false;
        public bool HasActiveErrors => false;
        public bool IsHistoryEnabled { get; set; }

        public IReadOnlyList<ActiveError> GetActiveErrors()
        {
            return [];
        }

        public IReadOnlyList<ErrorHistoryItem> GetHistory()
        {
            return codes
                .Select(code => new ErrorHistoryItem
                {
                    Code = code,
                    Description = code,
                    StartTime = DateTime.UtcNow
                })
                .ToList();
        }

        public void Raise(ErrorDefinition error, string? details = null)
        {
        }

        public void RaiseInStep(ErrorDefinition error, string stepId, string stepName, string? details = null)
        {
        }

        public void Clear(string errorCode)
        {
        }

        public void ClearActiveApplicationErrors()
        {
        }

        public void RaisePlc(ErrorDefinition error, string? stepId = null, string? stepName = null)
        {
        }

        public void ClearPlc(string errorCode)
        {
        }

        public void ClearAllActiveErrors()
        {
        }

        public void ClearHistory()
        {
        }
    }
}
