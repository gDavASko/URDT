# Audio API — KBPro (SFX / музыка / озвучка)

> Подтверждённое — из `HowToCreateModule.md` и `TrainCleaningModule_reference.md`. Помеченное «(сверь в исходнике)» — обязательно проверить сигнатуру в коде перед использованием, не выдумывать.

## SFX — `ISoundSystem` через LazySrv (в LogicSystem)
```csharp
private LazySrv<ISoundSystem> _sound = new LazySrv<ISoundSystem>();
// проигрывание:
_sound.Value.PlaySound("dirt_clean", isLoop: false, volume: 1f, MixerType.Sound);
// освобождение:
public override void Dispose() { _sound.Dispose(); base.Dispose(); }
```
- id звука — через `Constants.SOUNDS.*` (или задокументированный id), НЕ хардкод.
- `MixerType` — канал микшера (Sound/Music/Voice — точный enum сверь в исходнике).

## Музыка — `ISoundAccessor` / `_music` (в модуле)
```csharp
// в Startable():
_music?.SafePlay();
// в OnComplete()/Dispose():
_music?.SafeStop();
```
- Получение `ISoundAccessor` (сверь способ в исходнике: поле/инъекция/сервис).
- При завершении модуля музыку обязательно остановить, иначе наложение треков между мини-играми.

## Озвучка — выделенная VoiceSystem (паттерн из TrainCleaning)
- Отдельная `LogicSystem` (напр. `[Module]VoiceSystem`) играет реплики по стадиям: `start / tutor / wrong / praise / final`.
- **First-time флаги**: длинные обучающие реплики проигрываются один раз — флаг через `SaveService` (сверь API SaveService в исходнике).
- Реплики вызываются из FlowSystem в точках стадий, не разбросаны по компонентам.

## Чек аудио-задачи
- [ ] id в Constants, не хардкод.
- [ ] LazySrv + Dispose для ISoundSystem.
- [ ] музыка SafePlay/SafeStop, остановлена при выходе.
- [ ] озвучка в VoiceSystem, длинные реплики — first-time.
- [ ] сигнатуры (`PlaySound`, `MixerType`, `ISoundAccessor`, SaveService) сверены с исходником.
