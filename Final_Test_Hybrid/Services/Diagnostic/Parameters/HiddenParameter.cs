namespace Final_Test_Hybrid.Services.Diagnostic.Parameters;

/// <summary>
/// Скрытый параметр котла с двойной записью для режима стенда (п.4.6 протокола).
/// </summary>
/// <param name="Name">Имя параметра (напр. "3.1.A").</param>
/// <param name="Description">Описание параметра.</param>
/// <param name="ReadAddress">Адрес для чтения (основной регистр).</param>
/// <param name="Hidden1Address">Первый скрытый регистр для записи в режиме стенда.</param>
/// <param name="Hidden2Address">Второй скрытый регистр для записи в режиме стенда.</param>
public record HiddenParameter(
    string Name,
    string Description,
    ushort ReadAddress,
    ushort Hidden1Address,
    ushort Hidden2Address);
