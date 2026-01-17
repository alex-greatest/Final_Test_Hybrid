namespace Final_Test_Hybrid.Services.Storage;

public class TestResultStorageStub : ITestResultStorage
{
    public Task<SaveResult> SaveAsync(int testResult, CancellationToken ct)
    {
        return Task.FromResult(SaveResult.Success());
    }
}
