# Analytics Contract — KBPro события

> Точный API сервиса аналитики/статистики в проекте **обязательно сверить в исходнике** (`rg` по `IStatisticService`, `Analytics`, `SetValue`, `LogEvent`). Ниже — паттерн и контракт; конкретные сигнатуры пометь и подтверди.

## Доступ (через LazySrv, в модуле/системе)
```csharp
private LazySrv<IStatisticService> _stats = new LazySrv<IStatisticService>();
// пример из HowToCreateModule (сверь сигнатуру SetValue в исходнике):
_stats.Value.SetValue("CLEANING_DONE", true, excludeClear: false);
// освобождение:
public override void Dispose() { _stats.Dispose(); base.Dispose(); }
```
> Если в проекте отдельный сервис событий (`LogEvent(name, params)`) — использовать его; способ узнать: поиск по коду.

## Ключи — через Constants
- Не хардкодить строки событий: завести в `SOConstantsContainer` (группа событий/аналитики) → `Constants.<GROUP>.<EVENT>`.
- Имя события — стабильное, без персональных данных.

## Типовые точки модуля/экрана
| Событие | Точка вызова |
|---------|--------------|
| module_start | `Startable()` |
| module_complete | `OnComplete()` |
| key_action (напр. зуб просверлен, пятно очищено) | в соответствующем месте FlowSystem |
| screen_open / screen_action | в логике экрана |

## Чек аналитики
- [ ] доступ через LazySrv, `.Dispose()` в Dispose.
- [ ] ключи в Constants, не хардкод.
- [ ] события на верных точках lifecycle, без дублей.
- [ ] API сервиса (`SetValue`/`LogEvent`/иное) сверено с исходником.
- [ ] не логируются лишние/персональные данные.

## Таблица событий (заполнять под задачу)
| Событие | Ключ (Constants) | Точка вызова | Параметры |
|---------|------------------|--------------|-----------|
| … | `Constants.…` | … | … |
