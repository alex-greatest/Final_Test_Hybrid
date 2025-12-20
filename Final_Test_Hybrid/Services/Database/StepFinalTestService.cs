using Final_Test_Hybrid.Models.Database;
using Final_Test_Hybrid.Services.Database.Config;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Final_Test_Hybrid.Services.Database;

public class StepFinalTestService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    ILogger<StepFinalTestService> logger)
{
    public async Task<List<StepFinalTest>> GetAllAsync()
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        return await dbContext.StepFinalTests.AsNoTracking().ToListAsync();
    }

    public async Task<StepFinalTest> GetOrCreateByNameAsync(string name, CancellationToken ct = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(ct);
        var existing = await dbContext.StepFinalTests
            .FirstOrDefaultAsync(x => x.Name == name, ct);
        if (existing != null)
        {
            return existing;
        }
        return await CreateStepWithHistoryAsync(dbContext, name, ct);
    }

    private async Task<StepFinalTest> CreateStepWithHistoryAsync(
        AppDbContext dbContext,
        string name,
        CancellationToken ct)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(ct);
        try
        {
            var step = new StepFinalTest { Name = name };
            dbContext.StepFinalTests.Add(step);
            await dbContext.SaveChangesAsync(ct);
            var history = CreateHistoryRecord(step);
            dbContext.StepFinalTestHistories.Add(history);
            await dbContext.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            logger.LogInformation("Created StepFinalTest '{Name}' with Id {Id}", name, step.Id);
            return step;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(ct);
            logger.LogError(ex, "Failed to create StepFinalTest '{Name}'", name);
            throw new InvalidOperationException(DbConstraintErrorHandler.GetUserFriendlyMessage(ex), ex);
        }
    }

    public async Task<StepFinalTest> CreateAsync(StepFinalTest stepFinalTest)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        await using var transaction = await dbContext.Database.BeginTransactionAsync();
        try
        {
            dbContext.StepFinalTests.Add(stepFinalTest);
            await dbContext.SaveChangesAsync();
            var history = CreateHistoryRecord(stepFinalTest);
            dbContext.StepFinalTestHistories.Add(history);
            await dbContext.SaveChangesAsync();
            await transaction.CommitAsync();
            logger.LogInformation("Created StepFinalTest {Id} with active history", stepFinalTest.Id);
            return stepFinalTest;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            logger.LogError(ex, "Failed to create StepFinalTest");
            throw new InvalidOperationException(DbConstraintErrorHandler.GetUserFriendlyMessage(ex), ex);
        }
    }

    public async Task UpdateAsync(StepFinalTest stepFinalTest)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        await using var transaction = await dbContext.Database.BeginTransactionAsync();
        try
        {
            var existing = await FindExistingStepFinalTestAsync(dbContext, stepFinalTest.Id);
            existing.Name = stepFinalTest.Name;
            await DeactivateCurrentHistoryAsync(dbContext, stepFinalTest.Id);
            var newHistory = CreateHistoryRecord(stepFinalTest);
            dbContext.StepFinalTestHistories.Add(newHistory);
            await dbContext.SaveChangesAsync();
            await transaction.CommitAsync();
            logger.LogInformation("Updated StepFinalTest {Id} with new active history", stepFinalTest.Id);
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            logger.LogError(ex, "Failed to update StepFinalTest {Id}", stepFinalTest.Id);
            throw new InvalidOperationException(DbConstraintErrorHandler.GetUserFriendlyMessage(ex), ex);
        }
    }

    public async Task DeleteAsync(long id)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        await using var transaction = await dbContext.Database.BeginTransactionAsync();
        try
        {
            var stepFinalTest = await dbContext.StepFinalTests.FirstOrDefaultAsync(x => x.Id == id);
            if (stepFinalTest == null)
            {
                return;
            }
            await DeactivateCurrentHistoryAsync(dbContext, id);
            dbContext.StepFinalTests.Remove(stepFinalTest);
            await dbContext.SaveChangesAsync();
            await transaction.CommitAsync();
            logger.LogInformation("Deleted StepFinalTest {Id}", id);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            logger.LogError(ex, "Failed to delete StepFinalTest {Id}", id);
            throw new InvalidOperationException(DbConstraintErrorHandler.GetUserFriendlyMessage(ex), ex);
        }
    }

    private static StepFinalTestHistory CreateHistoryRecord(StepFinalTest stepFinalTest)
    {
        return new StepFinalTestHistory
        {
            StepFinalTestId = stepFinalTest.Id,
            Name = stepFinalTest.Name,
            IsActive = true
        };
    }

    private static async Task<StepFinalTest> FindExistingStepFinalTestAsync(AppDbContext dbContext, long id)
    {
        var existing = await dbContext.StepFinalTests.FirstOrDefaultAsync(x => x.Id == id);
        return existing ?? throw new InvalidOperationException("Шаг финального теста не найден");
    }

    private static async Task DeactivateCurrentHistoryAsync(AppDbContext dbContext, long stepFinalTestId)
    {
        var existingHistory = await dbContext.StepFinalTestHistories
            .FirstOrDefaultAsync(x => x.StepFinalTestId == stepFinalTestId && x.IsActive);
        existingHistory?.IsActive = false;
    }
}
