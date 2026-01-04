using Final_Test_Hybrid.Models.Steps;
using Final_Test_Hybrid.Services.Steps.Validation;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Interaces.PreExecution;

public interface IPreExecutionErrorDetails;

public record MissingPlcTagsDetails(IReadOnlyList<string> Tags) : IPreExecutionErrorDetails;

public record MissingRequiredTagsDetails(IReadOnlyList<string> Tags) : IPreExecutionErrorDetails;

public record UnknownStepsDetails(IReadOnlyList<UnknownStepInfo> Steps) : IPreExecutionErrorDetails;

public record MissingRecipesDetails(IReadOnlyList<MissingRecipeInfo> Recipes) : IPreExecutionErrorDetails;

public record RecipeWriteErrorDetails(IReadOnlyList<RecipeWriteErrorInfo> Errors) : IPreExecutionErrorDetails;
