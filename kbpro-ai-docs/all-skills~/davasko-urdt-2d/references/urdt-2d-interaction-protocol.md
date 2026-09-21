# Спецификация протокола URDT для 2D-окружения (URDT 2D Interaction Protocol)

Данный документ описывает сетевой протокол взаимодействия ИИ-агента с рантаймом Unity по WebSocket (`ws://127.0.0.1:7777/`).

---

## 1. Подключение и авторизация (Handshake)

Сервер URDT слушает TCP-порт `7777`. Сразу после установки WebSocket соединения клиент обязан отправить команду рукопожатия:

```json
{
  "api": 1,
  "id": "1",
  "action": "handshake",
  "payload": {
    "token": "urdt-test-poligon"
  }
}
```

**Ответ сервера:**
```json
{
  "api": 1,
  "id": "1",
  "type": "response",
  "status": "ok",
  "data": {
    "protocol_version": 1,
    "runtime": "Unity",
    "unity_version": "6000.4.4f1"
  }
}
```

---

## 2. Команды восприятия (Perception Commands)

### 1. `query` — Обнаружение активных целей
Возвращает список всех зарегистрированных маячков, удовлетворяющих селектору.

```json
{
  "api": 1,
  "id": "2",
  "action": "query",
  "payload": {
    "selector": {
      "activeOnly": true
    }
  }
}
```

**Ответ сервера:**
```json
{
  "api": 1,
  "id": "2",
  "type": "response",
  "status": "ok",
  "data": {
    "matches": [
      {
        "testId": "Item_Square",
        "name": "Item_Square",
        "activeInHierarchy": true,
        "screenPosition": { "x": 450.0, "y": 320.0 },
        "components": {
          "Urdt2DDraggableTarget": {
            "ItemId": "square",
            "IsJunk": false,
            "IsSnapped": false,
            "ScreenCenter": { "x": 450.0, "y": 320.0 }
          }
        }
      }
    ]
  }
}
```

### 2. `inspect` — Глубокий опрос состояния маячка
```json
{
  "api": 1,
  "id": "3",
  "action": "inspect",
  "payload": {
    "testId": "M01_SnapToSlot"
  }
}
```

---

## 3. Команды физического ввода (Action Commands)

### 1. `drag` — Непрерывное перетаскивание с интерполяцией
Служит для перемещения деталей в слоты, натяжения рогатки и управления ползунками.

*Синтаксис по TargetId:*
```json
{
  "api": 1,
  "id": "4",
  "action": "drag",
  "payload": {
    "from": "Item_Square",
    "to": "Slot_Square",
    "steps": 20
  }
}
```

*Синтаксис по координатам:*
```json
{
  "api": 1,
  "id": "5",
  "action": "drag",
  "payload": {
    "from": { "x": 450.0, "y": 320.0 },
    "to": { "x": 750.0, "y": 320.0 },
    "steps": 20
  }
}
```

> [!IMPORTANT]
> Параметр `steps` должен быть не менее 15–20! Одношаговый драг Unity `EventSystem` воспринимает как моментальный клик и возвращает деталь на место.

### 2. `click` — Одиночное дискретное нажатие
```json
{
  "api": 1,
  "id": "6",
  "action": "click",
  "payload": {
    "testId": "btn_2d_next"
  }
}
```

### 3. `pointer_down` и `pointer_up` — Длительное удержание
Необходимо для механик накопления заряда (M10), газа в машине (M27), розлива жидкостей (M31) и полета планера (M32).

```json
{
  "api": 1,
  "id": "7",
  "action": "pointer_down",
  "payload": {
    "testId": "HoldButton",
    "pointerId": 0
  }
}
```

После выдержки расчетного интервала времени ($T$ мс):
```json
{
  "api": 1,
  "id": "8",
  "action": "pointer_up",
  "payload": {
    "testId": "HoldButton",
    "pointerId": 0
  }
}
```

### 4. `press_move` — Сложные полигональные траектории и сплайны
Необходимо для обвода контуров (M29), прохождения каналов (M08), змейки очистки (M09), скретч-стирания (M23) и дугового вращения ручек (M12).

```json
{
  "api": 1,
  "id": "9",
  "action": "press_move",
  "payload": {
    "path": [
      { "x": 300.0, "y": 200.0 },
      { "x": 400.0, "y": 250.0 },
      { "x": 500.0, "y": 200.0 },
      { "x": 600.0, "y": 300.0 }
    ],
    "stepsPerSegment": 8
  }
}
```

---

## 4. Координатная система и независимость от разрешения

1. **Экранная система координат Unity**:
   - Начало координат $(0, 0)$ находится в **левом нижнем углу** окна Game View.
   - Ось $X$ направлена вправо.
   - Ось $Y$ направлена вверх.
2. **Динамический пересчет**:
   - Маячки URDT в свойстве `ScreenCenter` всегда возвращают актуальные текущие экранные координаты пикселей с учетом текущего размера окна Game View.
   - **НИКОГДА НЕ ИСПОЛЬЗУЙТЕ ЖЕСТКО ЗАШИТЫЕ КОНСТАНТЫ!** Всегда считывайте `item.ScreenCenter` и `slot.ScreenCenter` динамически непосредственно перед действием.
