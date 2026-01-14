namespace Final_Test_Hybrid.Services.Main.Messages;

public enum ExecutionPhase
{
    BarcodeReceived,
    ValidatingSteps,
    ValidatingRecipes,
    LoadingRecipes,
    CreatingDbRecords,
    WaitingForAdapter
}
