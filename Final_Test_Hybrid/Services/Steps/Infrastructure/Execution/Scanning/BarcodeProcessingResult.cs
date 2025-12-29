using Final_Test_Hybrid.Models.Steps;
using Final_Test_Hybrid.Services.Steps.Validation;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.Scanning;

public record BarcodeProcessingResult(
    bool IsSuccess,
    Guid ScanStepId,
    string? ErrorMessage = null,
    IReadOnlyList<string>? MissingPlcTags = null,
    IReadOnlyList<string>? MissingRequiredTags = null,
    IReadOnlyList<UnknownStepInfo>? UnknownSteps = null,
    IReadOnlyList<MissingRecipeInfo>? MissingRecipes = null)
{
    public static BarcodeProcessingResult Success(Guid scanStepId) =>
        new(true, scanStepId);

    public static BarcodeProcessingResult Fail(Guid scanStepId, string error) =>
        new(false, scanStepId, error);

    public static BarcodeProcessingResult WithMissingPlcTags(
        Guid scanStepId,
        string error,
        IReadOnlyList<string> missingTags) =>
        new(false, scanStepId, error, MissingPlcTags: missingTags);

    public static BarcodeProcessingResult WithMissingRequiredTags(
        Guid scanStepId,
        string error,
        IReadOnlyList<string> missingTags) =>
        new(false, scanStepId, error, MissingRequiredTags: missingTags);

    public static BarcodeProcessingResult WithUnknownSteps(
        Guid scanStepId,
        string error,
        IReadOnlyList<UnknownStepInfo> unknownSteps) =>
        new(false, scanStepId, error, UnknownSteps: unknownSteps);

    public static BarcodeProcessingResult WithMissingRecipes(
        Guid scanStepId,
        string error,
        IReadOnlyList<MissingRecipeInfo> missingRecipes) =>
        new(false, scanStepId, error, MissingRecipes: missingRecipes);
}
