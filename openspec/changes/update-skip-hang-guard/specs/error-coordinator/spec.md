## MODIFIED Requirements
### Requirement: WaitForResolution API
Система SHALL предоставлять единый метод ожидания решения оператора с опциональными параметрами через record. Для Skip при наличии BlockEndTag и BlockErrorTag и EnableSkip=true система MUST выполнять прямое чтение PLC-тегов перед ожиданиями по подписке, чтобы не терять короткие импульсы.

#### Scenario: Ожидание без параметров (базовый случай)
- **WHEN** вызывается `WaitForResolutionAsync()` без параметров
- **THEN** система ожидает сигналы Retry и Skip (через BaseTags)
- **AND** возвращает `ErrorResolution` по первому полученному сигналу

#### Scenario: Skip определяется по прямому чтению блоков
- **WHEN** вызывается `WaitForResolutionAsync(new WaitForResolutionOptions(BlockEndTag: "...", BlockErrorTag: "...", EnableSkip: true))`
- **AND** прямое чтение обоих тегов возвращает `true`
- **THEN** метод возвращает `ErrorResolution.Skip` без ожидания по подписке

#### Scenario: Ожидание с блоком и таймаутом
- **WHEN** вызывается `WaitForResolutionAsync(new WaitForResolutionOptions(BlockEndTag: "...", BlockErrorTag: "...", Timeout: TimeSpan.FromSeconds(30)))`
- **AND** прямое чтение блоков не дало Skip
- **THEN** система ожидает сигналы блока для Skip
- **AND** применяет указанный таймаут
- **AND** возвращает `ErrorResolution.Timeout` при истечении времени

#### Scenario: Ожидание без возможности Skip
- **WHEN** вызывается `WaitForResolutionAsync(new WaitForResolutionOptions(EnableSkip: false))`
- **THEN** система ожидает только сигнал Retry
- **AND** игнорирует сигналы Skip

## ADDED Requirements
### Requirement: Skip Signal Reset
Система SHALL перед ожиданием сброса сигналов Skip выполнять прямое чтение текущих значений BlockEndTag и BlockErrorTag. Если оба значения `false`, ожидание сброса завершается сразу. При таймауте ожидания система MUST логировать значения как из подписки, так и из прямого чтения.

#### Scenario: Сигналы уже сброшены
- **WHEN** прямое чтение BlockEndTag и BlockErrorTag возвращает `false`
- **THEN** ожидание сброса завершается без ожидания по подписке

#### Scenario: Таймаут ожидания сброса
- **WHEN** ожидание сброса сигналов Skip превышает таймаут
- **THEN** система логирует значения из подписки и из прямого чтения

### Requirement: Block Transition Hang Protection
Система SHALL ограничивать ожидание перехода блока (состояние "все колонки idle") таймаутом 10 секунд. При таймауте система MUST ставить выполнение на паузу, отправлять уведомление оператору "Тест завис. Нажмите на кнопку стоп." и не изменять результат теста на NOK/Failed.

#### Scenario: Переход блока завис
- **WHEN** ожидание "все колонки idle" длится более 10 секунд
- **THEN** система ставит выполнение на паузу
- **AND** отправляет уведомление оператору "Тест завис. Нажмите на кнопку стоп."
- **AND** результат теста не меняется на NOK/Failed
