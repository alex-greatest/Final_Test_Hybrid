<!-- OPENSPEC:START -->
# OpenSpec Instructions

These instructions are for AI assistants working in this project.

Always open `@/openspec/AGENTS.md` when the request:
- Mentions planning or proposals (words like proposal, spec, change, plan)
- Introduces new capabilities, breaking changes, architecture shifts, or big performance/security work
- Sounds ambiguous and you need the authoritative spec before coding

Use `@/openspec/AGENTS.md` to learn:
- How to create and apply change proposals
- Spec format and conventions
- Project structure and guidelines

Keep this managed block so 'openspec update' can refresh the instructions.

<!-- OPENSPEC:END -->

# Принципы разработки

## Чистый код (Clean Code)
- Пиши читаемый, понятный код с осмысленными именами переменных, методов и классов
- Методы должны быть короткими и выполнять одну задачу (Single Responsibility)
- Избегай дублирования кода (DRY - Don't Repeat Yourself)
- Используй понятные абстракции и избегай магических чисел/строк
- Пиши самодокументирующийся код - комментарии только там, где логика неочевидна

## Domain-Driven Design (DDD)
- Используй Ubiquitous Language - единый язык домена в коде и коммуникации
- Разделяй код на слои: Domain, Application, Infrastructure, Presentation
- Доменная логика должна быть в доменном слое, а не в сервисах или контроллерах
- Используй Value Objects для неизменяемых концепций домена
- Используй Entities для объектов с идентичностью
- Агрегаты должны защищать инварианты домена
- Репозитории работают только с агрегатами