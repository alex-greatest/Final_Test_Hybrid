го# PLC Tags Structure

## EU1 - Control Tags

| Name | Data Type | Address | Comment |
|------|-----------|---------|---------|
| PC_ON | Bool | %E0.0 | Управление включено (+24В) |
| 6K1_NOK | Bool | %E0.1 | 6К1 Токовые клещи. Проверка НОК. |
| 6K1_OK | Bool | %E0.2 | 6К1 Токовые клещи. Проверка ОК. |
| 6K2_NOK | Bool | %E0.3 | 6К2 Заземление не подключено |
| 6K2_OK | Bool | %E1.0 | 6К2 Заземление подключено |
| Plug_Ins | Bool | %E1.1 | Силовой кабель подключен |
| 7K3_ON | Bool | %E1.2 | 7К3 Разрешение подключения заземле... |
| Isometer_Alarm | Bool | %E1.3 | Ошибка изометра |
| TcabinetOK | Bool | %E2.0 | 6К2 Температура шкафа ОК |
| AirLevel_OK | Bool | %E2.1 | 25F2 Уровень сточных вод превышен |
| Trip_Ok | Bool | %E2.2 | Все автоматы питания включены |
| Schuko_Inside | Bool | %E2.3 | 7К2 Вилка вставлена |
| 7K2_CompOn | Bool | %A0.0 | 7К2 Компаратор включить |
| 7K1_BoilerOn | Bool | %A0.1 | 7К1 Питание котла включить |
| 7K4_NeutralOn | Bool | %A0.2 | 7К4 Заземление котла включить |
| 7K3_CurTongsOn | Bool | %A0.3 | 7К3 Токовые клещи включить |
| 7K5_SeishukoAdapter | Bool | %A1.0 | 7К5 Переключение Вилка-Адаптер |
| Blr_Supply | Int | %EW4 | Напряжение питания котла |
| GasP | Int | %EW10 | Давление газа |
| GasPa | Int | %EW8 | Атмосферное давление |

## EU2 - Gas and Temperature Sensors

| Name | Data Type | Address | Comment |
|------|-----------|---------|---------|
| FA_ReqMaintenance | Bool | %E12.0 | Газоанализатор. Требуется обслуживание |
| FA_RangeCO | Bool | %E12.1 | Газоанализатор. Диапазон CO |
| FA_RangeCO2 | Bool | %E12.2 | Газоанализатор. Диапазон CO2 |
| FA_CalUltramat23 | Bool | %E12.5 | Газоанализатор. Калибровка Ultramat 23 |
| FA_AlarmCondensat | Bool | %E13.0 | Газоанализатор. Неисправность Конденсата |
| FA_AlarmHeatLine | Bool | %E13.1 | Газоанализатор. Неисправность Линии нагрева |
| FA_AlarmGasCooler | Bool | %E13.2 | Газоанализатор. Неисправность Линии газа |
| FA_Trip | Bool | %E13.3 | Газоанализатор. Автоматы питания включены |
| FA_NoReady | Bool | %E14.0 | Газоанализатор. Нет готовности |
| E14_1 | Bool | %E14.1 | |
| FA_RangeO2 | Bool | %E14.2 | Газоанализатор. Измерение О2 |
| FA_RangeCO | Bool | — | Газоанализатор. Диапазон CO |
| FA_RangeCO2 | Bool | — | Газоанализатор. Диапазон CO2 |
| FA_ReqMaintenance | Bool | — | Газоанализатор. Требуется обслуживание |
| Adapter_Inserted | Bool | %E14.3 | 16K5 Адаптер вставлен |
| Adapter_NotInserted | Bool | %E15.0 | 16K5 Адаптер не вставлен |
| Boiler_Blocage | Bool | %E15.1 | 17К4 Котел заблокирован |
| Boiler_DeBlocage | Bool | %E15.2 | 17К5 Котел разблокирован |
| PlateIn | Bool | %E15.3 | 17К2 GRKPTS IN |
| FlexIn | Bool | %E15.3 | 17К2 GRKPTS IN (альтернативное имя) |
| FA_CO | Int | %EW20 | Газоанализатор. Выход CO |
| FA_CO2 | Int | %EW22 | Газоанализатор. Выход CO2 |
| FA_O2 | Int | %EW40 | Газоанализатор. Выход O2 |
| EV0_3_BoilerGasOn | Bool | %A12.0 | 18К11 Подача газа в котел |
| EV0_1_AirOn | Bool | %A12.1 | 18К14 Подача воздуха |
| EV0_1_LeakDefFlame | Bool | %A12.2 | 18К2 LEAK DETECTION FLAME GAS. Не подключено |
| RT_ThermoON | Bool | %A12.3 | 12К3 Питание термостата RT включить |
| FA_PumpON | Bool | %A13.0 | Газоанализатор. Включение насоса |
| FA_SelKCa1 | Bool | %A13.1 | Газоанализатор. Самокалибровка |
| POG_Gas_Flw | Int | %EW24 | POG Расход газа, л/мин |
| F5_DHW_Vtx_Flw | Int | %EW26 | F5 Расход воды в контуре DHW, л/мин |
| PAG_Blr_Gas_Press | Int | %EW28 | PAG Входное давление газа в котле |
| FE5_DHW_In_Press | Int | %EW30 | FE5 Давление потока воды в контуре DHW |
| PMB_CH_Flw_Press | Int | %EW32 | PMB Давление потока воды в контуре СН |
| POB_Brn_Gas_Press | Int | %EW34 | POB Давление газа на горелке, mBar |
| FR_CH_Flw_Press | Int | %EW36 | FR Расход воды в контуре СН, л/мин |
| RT_24V_Rm_Blr | Int | %EW38 | RT THERMOSTAT. Не подключен |
| VRP2_T | Int | %AW20 | SAMCON DHW Регулировка потока |
| VRP3_T | Int | %AW22 | 54K-TN Регулировка потока газа на входе |
| THG_Gaz_Sup_T | Int | %EW44 | THG Температура газа на входе в котел |
| TU5_DHW_Out_T | Int | %EW48 | TU5 Температура воды на выходе контура DHW |
| THA_CH_Pm_T | Int | %EW50 | THA Температура потока воды в контуре СН |
| TE2_DHW_In_T | Int | %EW46 | TE2 Температура воды на входе в контуре DHW |
| TRM_CH_Rm_T | Int | %EW52 | TRM Температура обратной воды в контуре СН |
| HeatingPumpON | Bool | %A14.0 | PUMP1 Насос подачи воды из теплообменника |
| ReqHeatingTank | Bool | %A14.1 | Heating Tank Request. Не подключён |
| POG1_Gas_Flw | Int | %EW42 | POG1 Расход газа, л/мин |

## EU3 - User Interface and Control Buttons

| Name | Data Type | Address | Comment |
|------|-----------|---------|---------|
| E56_0 | Bool | %E56.0 | Резерв |
| 30SB2 | Bool | %E56.1 | Кнопка Вправо |
| 30SB13 | Bool | %E56.2 | Кнопка Автомат выкл. |
| 30SB3 | Bool | %E56.3 | Кнопка Влево |
| 30SB4 | Bool | %E57.0 | Кнопка Ввверх |
| E57_1 | Bool | %E57.1 | Резерв |
| 30SB5 | Bool | %E57.2 | Кнопка Вниз |
| 30SB6 | Bool | %E57.3 | Кнопка Автомат вкл. |
| 30SA7_1 | Bool | %E58.0 | Селектор Авто |
| E58_1 | Bool | %E58.1 | Резерв |
| 30SA7_2 | Bool | %E58.2 | Селектор Авто |
| 30SA9 | Bool | %E58.3 | Селектор Тест Газ |
| 30SB10 | Bool | %E59.0 | Кнопка Zero Газ |
| E59_1 | Bool | %E59.1 | Резерв |
| E59_2 | Bool | %E59.2 | Резерв |
| 30SB11 | Bool | %E59.3 | Кнопка Стоп подачи газа |
| 51H1 | Bool | %A56.0 | Лампа Управление включено |
| 30H6 | Bool | %A56.1 | Лампа Авто вкл. |
| 51H2 | Bool | %A56.2 | Лампа Тест выполняется |
| 32H7 | Bool | %A56.3 | Лампа Одиночный шаг |
| E60_0 | Bool | %E60.0 | Резерв |
| E60_1 | Bool | %E60.1 | Резерв |
| 32S4 | Bool | %E60.2 | Кнопка Котел блокировать |
| 32S3 | Bool | %E60.3 | Кнопка Повтор теста |
| 32S5 | Bool | %E61.0 | Кнопка Включение насоса контура обогрева |
| 32S6 | Bool | %E61.1 | Кнопка Стоп |
| 32S7 | Bool | %E61.2 | Кнопка Одиночный шаг |

## UI - User Interface Tags (Extended)

| Name | Data Type | Address | Comment |
|------|-----------|---------|---------|
| PSW3_1 | Bool | %E64.0 | Давление воздуха ОК |
| F5VJ3_1 | Bool | %E64.0 | Давление воздуха ОК (альтернативное имя) |
| E64_1 | Bool | %E64.1 | Резерв |
| 28S2 | Bool | %E64.2 | Заслонка дымовая закрыта |
| 2BS2 | Bool | %E64.2 | Заслонка дымовая закрыта (альтернативное имя) |
| E64_3 | Bool | %E64.3 | Резерв |
| 28S1 | Bool | %E64.4 | Заслонка дымовая открыта |
| 2BS1 | Bool | %E64.4 | Заслонка дымовая открыта (альтернативное имя) |
| VPO_1 | Bool | %A64.1 | Подача газа C20 |
| VPO_3 | Bool | %A64.0 | Подача сжиженного газа |
| VP1_1 | Bool | %A64.3 | СН Продувка |
| VP1_2 | Bool | %A64.2 | СН Продувка |
| VP1_3 | Bool | %A64.5 | СН Заполнение |
| VP1_4 | Bool | %A64.4 | СН Быстрое заполнение |
| VP1_5 | Bool | %A64.7 | СН Подача воды в контур охлаждения |
| VP1_6 | Bool | %A64.6 | СН Подача воды в контур охлаждения |
| VP1_7 | Bool | %A65.1 | СН Подача горячей воды в теплообменник |
| VP1_8 | Bool | %A65.0 | СН Слив в обр. |
| VP1_9 | Bool | %A65.3 | Резерв |
| VP1_10 | Bool | %A65.2 | СН Подача горячей воды в теплообменник |
| VP1_11 | Bool | %A65.5 | СН Слив |
| VP1_12 | Bool | %A65.4 | СН Медленное заполнение |
| VP2_1 | Bool | %A65.7 | DHW Продувка |
| VP2_2 | Bool | %A65.6 | DHW Продувка |
| VP2_3 | Bool | %A66.1 | DHW Линия высокого давления |
| VP2_4 | Bool | %A66.0 | DHW Заполнение |
| VP2_5 | Bool | %A66.3 | DHW Выход из котла |
| VP2_6 | Bool | %A66.2 | DHW Слив |
| VP2_7 | Bool | %A66.5 | DHW Вход в котел |
| VP2_8 | Bool | %A66.4 | DHW Слив в обр. |
| I43_AAdapter | Bool | %A66.7 | Резерв? |
| VPO_2 | Bool | %A66.6 | Подача газа G25 |
| VP3_1b | Bool | %A68.0 | Подача воздуха, выкл. |
| VP3_1a | Bool | %A68.1 | Подача воздуха, факл. |
| VP3_3b | Bool | %A68.4 | Дымовую заслонку закрыть |
| VP3_3a | Bool | %A68.5 | Дымовую заслонку открыть |
| VP3_2b | Bool | %A68.6 | Котел разблокировать (Адаптер вниз) |
| VP3_2a | Bool | %A69.7 | Котел заблокировать (Адаптер вверх) |

---

**Note:** This structure represents PLC tag tables from a TIA Portal project for an industrial boiler control and testing system. The tags include:
- Control signals (EU1)
- Gas analyzer and temperature sensors (EU2) 
- User interface buttons and indicators (EU3, UI)
- Safety interlocks and status signals

All addresses use standard Siemens notation (%E for inputs, %A for outputs, %EW/%AW for word inputs/outputs).
