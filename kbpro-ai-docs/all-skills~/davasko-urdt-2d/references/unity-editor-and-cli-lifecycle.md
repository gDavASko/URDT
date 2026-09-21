# Управление жизненным циклом Unity через CLI и MCP (Unity Editor & CLI Lifecycle)

Данный документ описывает управление состоянием редактора Unity, переключение сцен и перевод в режим воспроизведения (Play Mode) для автономной работы с 2D-полигоном.

---

## 1. Проверка статуса редактора Unity

Перед отправкой любых WebSocket-команд агент обязан убедиться, что экземпляр Unity запущен и слушает управляющий порт:

```powershell
# Проверка официального Unity CLI
unity status
```

**Ожидаемый вывод:**
```
Port    State   Project             Version      PID
7800    ready   E:\Projects\URDT    6000.4.4f1   17392
```

Если редактор готов, статус отображается как `ready`.

---

## 2. Открытие сцены полигона

Для загрузки сцены 2D-полигона используется команда:

```powershell
unity command open_scene "Assets/URDT_TestPoligon/Scenes/URDT_TestPoligon_UI.unity"
```

---

## 3. Запуск режима Play Mode

Для входа в режим Play Mode:

```powershell
unity command editor_play
```

После входа в Play Mode:
1. Компонент `UrdtTestPoligonBootstrap` автоматически инициализирует сервер URDT.
2. Проверьте, что порт `7777` открыт и слушает входящие подключения:
```powershell
Get-NetTCPConnection -LocalPort 7777 -State Listen -ErrorAction SilentlyContinue
```
3. При положительном ответе агент готов подключаться по адресу `ws://127.0.0.1:7777/`.

### 3.1. Разрешение Game View и адаптивность (Resolution-Agnostic)
- Полигон поддерживает работу в любом текущем разрешении Game View. Координаты элементов рассчитываются динамически движком в реальных пикселях экрана.
- **Справка о legacy-оркестраторе**: в проекте присутствует редакторный класс `UrdtOrchestrator.cs`, который при наличии триггер-файла `Temp/UrdtPlay.trigger` или `URDT_AUTOPLAY=1` автоматически форсирует 1920×1080 (`PlayModeWindow.SetCustomRenderingResolution`). Современный ИИ-агент не требует обязательной фиксации разрешения и работает адаптивно в любом размере экрана.

---

## 4. Остановка Play Mode и выход

Для остановки воспроизведения (например, при необходимости пересборки сцены в Edit Mode):

```powershell
unity command editor_play
# или переключение флага в Editor
```

---

## 5. Восстановление после падений (Crash Recovery)

Если редактор Unity аварийно завершился или завис:
1. Перезапустите редактор (или попросите пользователя перезапустить его).
2. Выполните `unity status`, чтобы получить новый PID и подтвердить готовность порта 7800.
3. Откройте сцену: `unity command open_scene "Assets/URDT_TestPoligon/Scenes/URDT_TestPoligon_UI.unity"`.
4. Запустите Play Mode: `unity command editor_play`.
5. Подключитесь через `urdt-client.js`.
