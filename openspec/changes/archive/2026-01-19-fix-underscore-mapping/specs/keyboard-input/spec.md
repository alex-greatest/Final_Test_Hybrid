# Keyboard Input Mapping

## Overview

Маппинг виртуальных кодов клавиш в символы для обработки ввода со сканера штрихкодов.

## MODIFIED Requirements

### Requirement: MapSpecialKey method signature

Метод `MapSpecialKey` изменяется с `static` на instance для доступа к состоянию `_shiftPressed`.

**Before:**
```csharp
private static char? MapSpecialKey(ushort vKey)
```

**After:**
```csharp
private char? MapSpecialKey(ushort vKey)
```

## ADDED Requirements

### Requirement: Shift-aware OEM_MINUS mapping (0xBD)

#### Scenario: Underscore with Shift pressed
**Given** Shift нажат
**When** получен VKey `0xBD`
**Then** возвращается символ `_`

#### Scenario: Hyphen without Shift
**Given** Shift не нажат
**When** получен VKey `0xBD`
**Then** возвращается символ `-`

### Requirement: Shift-aware OEM_PLUS mapping (0xBB)

#### Scenario: Plus with Shift pressed
**Given** Shift нажат
**When** получен VKey `0xBB`
**Then** возвращается символ `+`

#### Scenario: Equals without Shift
**Given** Shift не нажат
**When** получен VKey `0xBB`
**Then** возвращается символ `=`

### Requirement: Shift-aware OEM_COMMA mapping (0xBC)

#### Scenario: Less-than with Shift pressed
**Given** Shift нажат
**When** получен VKey `0xBC`
**Then** возвращается символ `<`

#### Scenario: Comma without Shift
**Given** Shift не нажат
**When** получен VKey `0xBC`
**Then** возвращается символ `,`

### Requirement: Shift-aware OEM_PERIOD mapping (0xBE)

#### Scenario: Greater-than with Shift pressed
**Given** Shift нажат
**When** получен VKey `0xBE`
**Then** возвращается символ `>`

#### Scenario: Period without Shift
**Given** Shift не нажат
**When** получен VKey `0xBE`
**Then** возвращается символ `.`

### Requirement: Shift-aware OEM_SLASH mapping (0xBF)

#### Scenario: Question mark with Shift pressed
**Given** Shift нажат
**When** получен VKey `0xBF`
**Then** возвращается символ `?`

#### Scenario: Slash without Shift
**Given** Shift не нажат
**When** получен VKey `0xBF`
**Then** возвращается символ `/`
