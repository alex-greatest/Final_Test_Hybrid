using Final_Test_Hybrid.Services.Main.PlcReset;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.ErrorCoordinator;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.Completion;

/// <summary>
/// Состояние UI для отображения изображения результата теста.
/// Используется в MyComponent.razor для переключения между grid и картинкой.
/// Подписывается на события сброса для скрытия изображения.
/// </summary>
public class TestCompletionUiState
{
    private const string OkImagePath = "images/green_smiley_clean.png";
    private const string NokImagePath = "images/red_smile.jpg";

    private readonly Lock _lock = new();
    private readonly PlcResetCoordinator _plcResetCoordinator;
    private readonly IErrorCoordinator _errorCoordinator;

    private bool _showResultImage;
    private int _testResult;

    public TestCompletionUiState(
        PlcResetCoordinator plcResetCoordinator,
        IErrorCoordinator errorCoordinator)
    {
        _plcResetCoordinator = plcResetCoordinator;
        _errorCoordinator = errorCoordinator;

        // Подписка на сбросы — скрыть изображение при любом сбросе
        _plcResetCoordinator.OnForceStop += HideImage;
        _errorCoordinator.OnReset += HideImage;
    }

    /// <summary>
    /// Показывать ли изображение результата вместо грида.
    /// </summary>
    public bool ShowResultImage
    {
        get { lock (_lock) return _showResultImage; }
    }

    /// <summary>
    /// Результат теста (1 = OK, 2 = NOK).
    /// </summary>
    public int TestResult
    {
        get { lock (_lock) return _testResult; }
    }

    /// <summary>
    /// Путь к изображению результата:
    /// 1 = OK (зелёная), 2 = NOK (красная),
    /// и fallback на NOK для невалидных значений.
    /// </summary>
    public string ImagePath
    {
        get
        {
            lock (_lock)
            {
                return _testResult switch
                {
                    1 => OkImagePath,
                    _ => NokImagePath
                };
            }
        }
    }

    /// <summary>
    /// Текст инструкции для оператора.
    /// </summary>
    public string InstructionText =>
        "\"Один шаг\" - закончить тест или \"Повтор\" для повтора теста";

    /// <summary>
    /// Событие изменения состояния (для обновления UI).
    /// </summary>
    public event Action? OnStateChanged;

    /// <summary>
    /// Показать изображение результата.
    /// </summary>
    public void ShowImage(int testResult)
    {
        lock (_lock)
        {
            _testResult = testResult;
            _showResultImage = true;
        }
        OnStateChanged?.Invoke();
    }

    /// <summary>
    /// Скрыть изображение результата.
    /// </summary>
    public void HideImage()
    {
        lock (_lock)
        {
            _showResultImage = false;
        }
        OnStateChanged?.Invoke();
    }

    /// <summary>
    /// Очистить состояние (для сбросов).
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _showResultImage = false;
            _testResult = 0;
        }
        OnStateChanged?.Invoke();
    }
}
