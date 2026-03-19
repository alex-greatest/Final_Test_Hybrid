namespace Final_Test_Hybrid.Services.Diagnostic.Models;

/// <summary>
/// Классификация причины неуспешной диагностической операции.
/// </summary>
public enum DiagnosticFailureKind
{
    None = 0,
    Communication = 1,
    Functional = 2
}
