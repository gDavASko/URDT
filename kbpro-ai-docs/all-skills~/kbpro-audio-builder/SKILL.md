---
name: kbpro-audio-builder
description: "Audio/SFX/music (ISoundSystem). Triggers: add sound, play sfx, озвучка, voice over, музыка модуля, ISoundSystem, VoiceSystem."
status: candidate
owner: KBPro
license: project-internal
allowed_tools:
  - filesystem-read
  - filesystem-write
  - rg
required_reading:
  - references/audio-api.md
known_risks:
  - Хардкод строковых id звуков вместо Constants.SOUNDS.*.
  - Прямой ServiceLocator вместо LazySrv<ISoundSystem>; не вызван .Dispose().
  - Музыка не остановлена/не освобождена в Dispose (утечка, наложение треков между модулями).
  - Сигнатуры аудио-API выдуманы — нужно сверять по references/audio-api.md.
  - Озвучка без first-time флагов (повтор длинных реплик каждый раз).
---

# KBPro Audio Builder

## Purpose

Интегрировать звук в модуль KBPro по архитектуре проекта: **SFX** (ISoundSystem), **музыка** (ISoundAccessor), **озвучка/реплики** (VoiceSystem с first-time флагами). Покрывает функционалы задач `звуки` и `озвучка`.

## When To Use

* Задачи `[Dev][звуки] …` / `[Dev][озвучка] …`.
* Добавить звук действия, музыку модуля, вступительные/похвальные/ошибочные реплики персонажей.

## Do Not Use

* Для проигрывания анимаций/VFX — `unity-animation-visuals-expert` / `unity-vfx-particle-author`.
* Для импорта аудио-ассетов в Unity — это Editor-операция вне кода.

## Core Rules

1. **Только через LazySrv:** `LazySrv<ISoundSystem>` для SFX, `ISoundAccessor`/`_music` для музыки; в `Dispose()` — `.Dispose()` / `SafeStop()`.
2. **Никаких хардкод-строк id** — только `Constants.SOUNDS.*` (или задокументированные id). См. [audio-api.md](file://references/audio-api.md).
3. **Озвучка — отдельная система** (VoiceSystem), а не разбросанные вызовы; длинные реплики гейтятся **first-time флагами** (через SaveService).
4. **SFX из LogicSystem**, не из GameComponent (компонент — только вью).
5. **Сигнатуры не выдумывать** — сверять по audio-api.md / исходнику `ISoundSystem`/`ISoundAccessor`.

## Workflow

1. Определи по ТЗ: какие SFX (на какие действия), нужна ли музыка модуля, какие реплики (старт/тутор/успех/ошибка/финал) и какие — first-time.
2. Заведи нужные `Constants.SOUNDS.*` (или зафиксируй id для заведения пользователем).
3. SFX: в нужной `LogicSystem` добавь `LazySrv<ISoundSystem>` и вызовы `PlaySound(...)`; освободи в `Dispose`.
4. Музыка: в модуле `_music.SafePlay()` в `Startable()`, `SafeStop()` в `OnComplete/Dispose`.
5. Озвучка: создай/дополни VoiceSystem (реплики по стадиям + first-time флаги); вызывай из флоу.
6. Проверь изоляцию: все звуки останавливаются и освобождаются при выходе из модуля.

## Output Format

* Изменённые/новые `LogicSystem` с аудио-вызовами + VoiceSystem; список нужных `Constants.SOUNDS.*`.
* Заметка: какие id звуков/реплик нужно положить в аудио-банк (для пользователя/звукаря).

## Test Prompts

1. «Добавь звук очистки пятна и музыку модуля чистки; всё освободи в Dispose.»
2. «Сделай озвучку этапа Диагностики: вступление, подсказка о диагнозе, «проверь зеркалом» — с first-time флагами.»
3. «Вынеси разрозненные PlaySound в VoiceSystem по стадиям.»
