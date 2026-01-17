namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.Completion;

/// <summary>
/// Состояние UI для отображения изображения результата теста.
/// Используется в MyComponent.razor для переключения между grid и картинкой.
/// </summary>
public class TestCompletionUiState
{
    private readonly Lock _lock = new();

    private bool _showResultImage;
    private int _testResult;

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
    /// Путь к изображению результата.
    /// </summary>
    public string ImagePath => "images/green_smiley_clean.png";

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
