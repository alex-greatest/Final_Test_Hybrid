using Final_Test_Hybrid.Components.Main;

namespace Final_Test_Hybrid.Tests.Runtime;

public sealed class BoilerInfoInputDraftTests
{
    [Fact]
    public void SyncFromPreserved_FillsDraft_WhenPreservedValueAppears()
    {
        var draft = new BoilerInfoInputDraft();

        draft.SyncFromPreserved("SN-001");

        Assert.Equal("SN-001", draft.Draft);
    }

    [Fact]
    public void SyncFromPreserved_DoesNotOverwriteManualEdit_WhenPreservedValueDidNotChange()
    {
        var draft = new BoilerInfoInputDraft();
        draft.SyncFromPreserved("SN-001");

        draft.Update("SN-001X");
        draft.SyncFromPreserved("SN-001");

        Assert.Equal("SN-001X", draft.Draft);
    }

    [Fact]
    public void GetSubmitValue_DoesNotFallbackToOldPreservedValue_AfterManualClear()
    {
        var draft = new BoilerInfoInputDraft();
        draft.SyncFromPreserved("SN-001");

        draft.Update(string.Empty);
        draft.SyncFromPreserved("SN-001");

        Assert.Equal(string.Empty, draft.Draft);
        Assert.Null(draft.GetSubmitValue());
    }

    [Fact]
    public void SyncFromPreserved_UpdatesDraft_WhenNewPreservedValueArrives()
    {
        var draft = new BoilerInfoInputDraft();
        draft.SyncFromPreserved("SN-001");

        draft.Update("temporary-edit");
        draft.SyncFromPreserved("SN-002");

        Assert.Equal("SN-002", draft.Draft);
    }
}
