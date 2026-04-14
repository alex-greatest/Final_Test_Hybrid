using Final_Test_Hybrid.Components.Main.Modals.Rework;
using Final_Test_Hybrid.Services.SpringBoot.Operation.Interrupt;

namespace Final_Test_Hybrid.Tests.Runtime;

public class InterruptFlowExecutorTests
{
    [Fact]
    public async Task ExecuteAsync_UsesAdminUsername_WhenAdminAuthIsRequired()
    {
        var dialogService = new FakeInterruptDialogService
        {
            AuthResult = new AdminAuthResult
            {
                Success = true,
                Username = "admin-user"
            },
            ReasonHandler = (onSubmit, ct) => onSubmit("interrupt reason", ct)
        };
        var executor = new InterruptFlowExecutor();
        string? submittedUsername = null;
        string? submittedReason = null;

        var result = await executor.ExecuteAsync(
            dialogService,
            (username, reason, _) =>
            {
                submittedUsername = username;
                submittedReason = reason;
                return Task.FromResult(SaveResult.Success());
            },
            requireAdminAuth: true,
            operatorUsername: "operator-user",
            showCancelButton: true,
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.IsCancelled);
        Assert.Equal("admin-user", result.AdminUsername);
        Assert.Equal("admin-user", submittedUsername);
        Assert.Equal("interrupt reason", submittedReason);
        Assert.Equal(1, dialogService.ShowAdminAuthCalls);
        Assert.Equal(1, dialogService.ShowInterruptReasonCalls);
        Assert.True(dialogService.LastAdminAuthShowCancelButton);
        Assert.True(dialogService.LastAdminAuthRequireProtectedCancel);
        Assert.True(dialogService.LastReasonShowCancelButton);
    }

    [Fact]
    public async Task ExecuteAsync_Cancels_WhenAdminAuthDialogIsClosed()
    {
        var dialogService = new FakeInterruptDialogService();
        var executor = new InterruptFlowExecutor();
        var saveCalled = false;

        var result = await executor.ExecuteAsync(
            dialogService,
            (_, _, _) =>
            {
                saveCalled = true;
                return Task.FromResult(SaveResult.Success());
            },
            requireAdminAuth: true,
            operatorUsername: "operator-user",
            showCancelButton: true,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.True(result.IsCancelled);
        Assert.False(saveCalled);
        Assert.Equal(1, dialogService.ShowAdminAuthCalls);
        Assert.Equal(0, dialogService.ShowInterruptReasonCalls);
    }

    [Fact]
    public async Task ExecuteAsync_UsesOperatorUsername_WhenAdminAuthIsNotRequired()
    {
        var dialogService = new FakeInterruptDialogService
        {
            ReasonHandler = (onSubmit, ct) => onSubmit("interrupt reason", ct)
        };
        var executor = new InterruptFlowExecutor();
        string? submittedUsername = null;

        var result = await executor.ExecuteAsync(
            dialogService,
            (username, _, _) =>
            {
                submittedUsername = username;
                return Task.FromResult(SaveResult.Success());
            },
            requireAdminAuth: false,
            operatorUsername: "operator-user",
            showCancelButton: true,
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.IsCancelled);
        Assert.Equal("operator-user", result.AdminUsername);
        Assert.Equal("operator-user", submittedUsername);
        Assert.Equal(0, dialogService.ShowAdminAuthCalls);
        Assert.Equal(1, dialogService.ShowInterruptReasonCalls);
        Assert.True(dialogService.LastReasonShowCancelButton);
    }

    [Fact]
    public async Task ExecuteAsync_HidesCancelButtons_WhenRepeatSaveModeIsUsed()
    {
        var dialogService = new FakeInterruptDialogService
        {
            AuthResult = new AdminAuthResult
            {
                Success = true,
                Username = "admin-user"
            },
            ReasonHandler = (onSubmit, ct) => onSubmit("repeat reason", ct)
        };
        var executor = new InterruptFlowExecutor();

        var result = await executor.ExecuteAsync(
            dialogService,
            (_, _, _) => Task.FromResult(SaveResult.Success()),
            requireAdminAuth: true,
            operatorUsername: "operator-user",
            showCancelButton: false,
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(dialogService.LastAdminAuthShowCancelButton);
        Assert.False(dialogService.LastAdminAuthRequireProtectedCancel);
        Assert.False(dialogService.LastReasonShowCancelButton);
    }

    private sealed class FakeInterruptDialogService : InterruptDialogService
    {
        public FakeInterruptDialogService()
            : base(null!)
        {
        }

        public AdminAuthResult? AuthResult { get; init; }

        public Func<Func<string, CancellationToken, Task<SaveResult>>, CancellationToken, Task<SaveResult>>?
            ReasonHandler { get; init; }

        public int ShowAdminAuthCalls { get; private set; }

        public int ShowInterruptReasonCalls { get; private set; }

        public bool LastAdminAuthShowCancelButton { get; private set; }

        public bool LastAdminAuthRequireProtectedCancel { get; private set; }

        public bool LastReasonShowCancelButton { get; private set; }

        public override Task<AdminAuthResult?> ShowAdminAuthAsync(
            bool showCancelButton = true,
            bool requireProtectedCancel = true)
        {
            ShowAdminAuthCalls++;
            LastAdminAuthShowCancelButton = showCancelButton;
            LastAdminAuthRequireProtectedCancel = requireProtectedCancel;
            return Task.FromResult(AuthResult);
        }

        public override async Task<SaveResult?> ShowInterruptReasonAsync(
            Func<string, CancellationToken, Task<SaveResult>> onSubmit,
            CancellationToken ct,
            bool showCancelButton = true)
        {
            ShowInterruptReasonCalls++;
            LastReasonShowCancelButton = showCancelButton;
            return ReasonHandler == null ? null : await ReasonHandler(onSubmit, ct);
        }
    }
}
