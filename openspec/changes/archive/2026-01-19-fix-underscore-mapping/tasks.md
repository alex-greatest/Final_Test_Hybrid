# Tasks: Fix Keyboard Input Mapping

## Implementation

1. [x] **Изменить `MapSpecialKey` на instance метод**
   - Убрать `static` модификатор
   - Добавить использование `_shiftPressed`

2. [x] **Исправить маппинг всех OEM клавиш с Shift**
   - `0xBD`: `_shiftPressed ? '_' : '-'`
   - `0xBB`: `_shiftPressed ? '+' : '='`
   - `0xBC`: `_shiftPressed ? '<' : ','`
   - `0xBE`: `_shiftPressed ? '>' : '.'`
   - `0xBF`: `_shiftPressed ? '?' : '/'`

## Validation

3. [x] **Build verification**
   - `dotnet build` без ошибок

4. [x] **Ручное тестирование**
   - Сканировать штрихкод с `_`
   - Сканировать штрихкод с `-`
   - Сканировать штрихкод с `=`
   - Сканировать штрихкод с `+`
