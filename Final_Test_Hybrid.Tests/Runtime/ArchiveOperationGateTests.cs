using System.Reflection;
using Final_Test_Hybrid.Components.Archive;
using Final_Test_Hybrid.Models.Database;
using Final_Test_Hybrid.Tests.TestSupport;

namespace Final_Test_Hybrid.Tests.Runtime;

public class ArchiveOperationGateTests
{
    [Theory]
    [InlineData(OperationResultStatus.Ok, true)]
    [InlineData(OperationResultStatus.Nok, true)]
    [InlineData(OperationResultStatus.Interrupted, true)]
    [InlineData(OperationResultStatus.InWork, false)]
    public void ArchiveGrid_CanOpenArchiveDetails_MatchesExpectedStatuses(
        OperationResultStatus status,
        bool expected)
    {
        var result = InvokeStaticGate<ArchiveGrid>("CanOpenArchiveDetails", status);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(OperationResultStatus.Ok, true)]
    [InlineData(OperationResultStatus.Nok, true)]
    [InlineData(OperationResultStatus.Interrupted, true)]
    [InlineData(OperationResultStatus.InWork, false)]
    public void OperationDetailsDialog_CanExportArchive_MatchesExpectedStatuses(
        OperationResultStatus status,
        bool expected)
    {
        var result = InvokeStaticGate<OperationDetailsDialog>("CanExportArchive", status);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void OperationDetailsDialog_CanExport_AllowsInterruptedWhenNotExporting()
    {
        var dialog = new OperationDetailsDialog();
        SetOperation(dialog, CreateOperation(OperationResultStatus.Interrupted));

        var canExport = InvokeInstanceGate(dialog, "CanExport");

        Assert.True(canExport);
    }

    [Fact]
    public void OperationDetailsDialog_CanExport_BlocksInterruptedWhileExportInProgress()
    {
        var dialog = new OperationDetailsDialog();
        SetOperation(dialog, CreateOperation(OperationResultStatus.Interrupted));
        TestInfrastructure.SetPrivateField(dialog, "_isExporting", true);

        var canExport = InvokeInstanceGate(dialog, "CanExport");

        Assert.False(canExport);
    }

    private static bool InvokeStaticGate<TComponent>(string methodName, OperationResultStatus status)
    {
        var method = typeof(TComponent).GetMethod(
            methodName,
            BindingFlags.Static | BindingFlags.NonPublic);

        return Assert.IsType<bool>(method!.Invoke(null, [status]));
    }

    private static bool InvokeInstanceGate(object instance, string propertyName)
    {
        var property = instance.GetType().GetProperty(
            propertyName,
            BindingFlags.Instance | BindingFlags.NonPublic);

        return Assert.IsType<bool>(property!.GetValue(instance));
    }

    private static void SetOperation(OperationDetailsDialog dialog, Operation operation)
    {
        var property = typeof(OperationDetailsDialog).GetProperty(nameof(OperationDetailsDialog.Operation));
        property!.SetValue(dialog, operation);
    }

    private static Operation CreateOperation(OperationResultStatus status)
    {
        return new Operation
        {
            DateStart = DateTime.UtcNow,
            Status = status,
            NumberShift = 1,
            Version = 1,
            Operator = "operator-1",
            Boiler = new Boiler
            {
                SerialNumber = "SN-001",
                BoilerTypeCycleId = 1,
                DateCreate = DateTime.UtcNow,
                Status = status,
                Operator = "operator-1"
            }
        };
    }
}
