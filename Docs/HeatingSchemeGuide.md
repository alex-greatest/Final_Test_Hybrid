# HeatingScheme - Мнемосхема отопления

## Обзор

`HeatingScheme.razor` — SVG-мнемосхема системы отопления с динамической визуализацией состояния клапанов и труб.

## Архитектура градиентов

### Базовые градиенты

| ID | Тип | Описание |
|----|-----|----------|
| `h_elem_393` | Серый | Базовый градиент для закрытых труб |
| `h_elem_393_green` | Зелёный | Базовый градиент для открытых труб (VP1_5, VP1_6) |
| `h_elem_393_red` | Красный | Базовый градиент для открытых труб (VP1_3, VP1_4, VP1_7) |

### Производные градиенты

Каждая труба использует свой градиент, который ссылается на базовый:

```xml
<!-- Серый (закрытое состояние) -->
<linearGradient id="h_elem_107" xlink:href="#h_elem_393"/>

<!-- Зелёный (открытое состояние) -->
<linearGradient id="h_elem_107_green" xlink:href="#h_elem_393_green"/>
```

## Привязка труб к клапанам

### VP1_5 (одиночная зависимость)

| Труба | Градиент | Метод |
|-------|----------|-------|
| h_elem_400 | h_elem_39 | `GetPipeGradient_h_elem_400()` |
| h_elem_402 | h_elem_50 | `GetPipeGradient_h_elem_402()` |
| h_elem_363 | h_elem_107 | `GetPipeGradient_h_elem_363()` |

### VP1_6 (одиночная зависимость)

| Труба | Градиент | Метод |
|-------|----------|-------|
| h_elem_419 | h_elem_175 | `GetPipeGradient_h_elem_419()` |
| h_elem_413 | h_elem_225 | `GetPipeGradient_h_elem_413()` |
| h_elem_428 | h_elem_393 | `GetPipeGradient_h_elem_428()` |
| h_elem_422 | h_elem_271 | `GetPipeGradient_h_elem_422()` |

### VP1_5 ИЛИ VP1_6 (комбинированная зависимость)

| Труба | Градиент | Метод |
|-------|----------|-------|
| h_elem_401 | h_elem_141 | `GetPipeGradient_h_elem_401()` |
| h_elem_426 | h_elem_241 | `GetPipeGradient_h_elem_426()` |

### VP1_7 (серый/красный)

| Труба | Градиент | Метод |
|-------|----------|-------|
| h_elem_407 | h_elem_101 | `GetPipeGradient_h_elem_407()` |
| h_elem_372 | h_elem_21 | `GetPipeGradient_h_elem_372()` |

### VP1_10 (серый/красный)

| Труба | Градиент | Метод |
|-------|----------|-------|
| h_elem_356 | h_elem_135 | `GetPipeGradient_h_elem_356()` |
| h_elem_366 | h_elem_63 | `GetPipeGradient_h_elem_366()` |
| h_elem_359 | h_elem_139 | `GetPipeGradient_h_elem_359()` |

## Добавление новой трубы

### 1. Создать зелёный градиент

После оригинального градиента добавить зелёную версию:

```xml
<!-- Зелёная версия h_elem_XXX для h_elem_YYY -->
<linearGradient id="h_elem_XXX_green"
                x1="..." y1="..." x2="..." y2="..."
                xlink:href="#h_elem_393_green"/>
```

### 2. Добавить константы

```csharp
private const string GrayGradient_h_elem_XXX = "url(#h_elem_XXX)";
private const string GreenGradient_h_elem_XXX = "url(#h_elem_XXX_green)";
```

### 3. Создать метод

**Одиночная зависимость:**
```csharp
/// <summary>
/// Получает градиент для трубы h_elem_YYY (VP1_5).
/// </summary>
private string GetPipeGradient_h_elem_YYY() =>
    _valveStates.TryGetValue("VP1_5", out var isOpen) && isOpen
        ? GreenGradient_h_elem_XXX : GrayGradient_h_elem_XXX;
```

**Комбинированная зависимость (OR):**
```csharp
/// <summary>
/// Получает градиент для трубы h_elem_YYY (VP1_5 и VP1_6).
/// </summary>
private string GetPipeGradient_h_elem_YYY() =>
    (_valveStates.TryGetValue("VP1_5", out var isOpen5) && isOpen5) ||
    (_valveStates.TryGetValue("VP1_6", out var isOpen6) && isOpen6)
        ? GreenGradient_h_elem_XXX : GrayGradient_h_elem_XXX;
```

### 4. Обновить SVG элемент

```xml
<path d="..." style="fill: @GetPipeGradient_h_elem_YYY();"/>
```

## Создание красных градиентов (серый/красный)

Для клапанов VP1_3, VP1_4, VP1_7 используется красная цветовая схема (горячая вода).

### Базовый красный градиент

Базовый красный градиент `h_elem_393_red` уже определён в `<defs>`:

```xml
<linearGradient id="h_elem_393_red" data-name="Красный градиент 33"
                x1="-2278.357" y1="1124.636" x2="-2251.661" y2="1124.636"
                gradientTransform="translate(306.518 292.952) rotate(-180) scale(.569 -.569)"
                gradientUnits="userSpaceOnUse">
    <stop offset=".01" stop-color="#b02020"/>
    <stop offset=".18" stop-color="#e14a4a"/>
    <stop offset=".5" stop-color="#ffd0d0"/>
    <stop offset="1" stop-color="#b02020"/>
</linearGradient>
```

### Создание производного красного градиента

1. Найти оригинальный серый градиент (например, `h_elem_101`):
```xml
<linearGradient id="h_elem_101" data-name="Безымянный градиент 33"
                x1="-2050.254" y1="745.276" x2="-2023.558" y2="745.276"
                xlink:href="#h_elem_393"/>
```

2. Создать красную версию, скопировав координаты и заменив `xlink:href`:
```xml
<!-- Красная версия h_elem_101 для h_elem_407 (VP1_7) -->
<linearGradient id="h_elem_101_red"
                x1="-2050.254" y1="745.276" x2="-2023.558" y2="745.276"
                xlink:href="#h_elem_393_red"/>
```

### Пример для VP1_7

```csharp
// Константы
private const string GrayGradient_h_elem_101 = "url(#h_elem_101)";
private const string RedGradient_h_elem_101 = "url(#h_elem_101_red)";

// Метод
private string GetPipeGradient_h_elem_407() =>
    _valveStates.TryGetValue("VP1_7", out var isOpen) && isOpen
        ? RedGradient_h_elem_101 : GrayGradient_h_elem_101;
```

## Состояние клапанов

Состояния хранятся в `_valveStates`:

```csharp
private readonly Dictionary<string, bool> _valveStates = new();
```

Обновление происходит через `UpdateValveState(string valveName, bool isOpen)`.
