# HeatingScheme - Мнемосхема отопления

## Обзор

`HeatingScheme.razor` — SVG-мнемосхема системы отопления с динамической визуализацией состояния клапанов и труб.

## Архитектура градиентов

### Базовые градиенты

| ID | Тип | Описание |
|----|-----|----------|
| `h_elem_393` | Серый | Базовый градиент для закрытых труб |
| `h_elem_393_green` | Зелёный | Базовый градиент для открытых труб |

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

## Состояние клапанов

Состояния хранятся в `_valveStates`:

```csharp
private readonly Dictionary<string, bool> _valveStates = new();
```

Обновление происходит через `UpdateValveState(string valveName, bool isOpen)`.
