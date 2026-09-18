# URDT WebSocket Protocol Specification

## Connection & Handshake

- **Endpoint**: `ws://127.0.0.1:7777/`
- **Protocol**: JSON-RPC over WebSocket frames (`api: 1`).

### Handshake Request
```json
{
  "api": 1,
  "id": "req-1",
  "action": "handshake",
  "payload": {
    "token": "urdt-test-poligon"
  }
}
```

### Handshake Response
```json
{
  "api": 1,
  "id": "req-1",
  "status": "ready",
  "data": {
    "server_version": "0.1.0",
    "origin": "bottom-left",
    "screen": { "width": 1920, "height": 1080 },
    "processId": 47020,
    "port": 7777
  }
}
```

---

## Action Commands

### 1. `query`
Finds targets matching component type, tag, or name.
```json
{
  "api": 1,
  "id": "req-2",
  "action": "query",
  "payload": {
    "byComponent": "UrdtUiButtonTarget"
  }
}
```
**Response**:
```json
{
  "api": 1,
  "id": "req-2",
  "status": "ok",
  "data": {
    "count": 1,
    "matches": [
      {
        "testId": "btn_open_ui_suite",
        "name": "OpenUiSuiteButton",
        "activeInHierarchy": true,
        "components": {
          "UrdtUiButtonTarget": {
            "TargetId": "btn_open_ui_suite",
            "TargetKind": "button",
            "IsInteractable": true,
            "ScreenRect": "(x:66.00, y:529.50, width:1788.00, height:96.00)",
            "ScreenCenter": { "x": 960, "y": 577.5 },
            "InteractionCount": 1
          }
        }
      }
    ]
  }
}
```

### 2. `inspect`
Reads full runtime state of a specific target by `testId` or `handle`.
```json
{
  "api": 1,
  "id": "req-3",
  "action": "inspect",
  "payload": {
    "testId": "ui.state_toggle"
  }
}
```

### 3. `click`
Simulates a device-level mouse click or touch tap at the target's live center.
```json
{
  "api": 1,
  "id": "req-4",
  "action": "click",
  "payload": {
    "testId": "ui.primary_button"
  }
}
```

### 4. `double_click`
Simulates two rapid clicks within the OS double-click time window.
```json
{
  "api": 1,
  "id": "req-5",
  "action": "double_click",
  "payload": {
    "testId": "ui.primary_button"
  }
}
```

### 5. `drag`
Simulates dragging across screen space from a start point to an end point.
```json
{
  "api": 1,
  "id": "req-6",
  "action": "drag",
  "payload": {
    "testId": "ui.value_slider",
    "startX": 500,
    "startY": 569,
    "endX": 1200,
    "endY": 569,
    "durationMs": 400
  }
}
```

### 6. `scroll`
Simulates mouse wheel scrolling or pan scrolling.
```json
{
  "api": 1,
  "id": "req-7",
  "action": "scroll",
  "payload": {
    "testId": "ui.command_scroll",
    "deltaX": 0,
    "deltaY": -120
  }
}
```

### 7. `type_text`
Simulates physical typing of a string character-by-character into the active input element.
```json
{
  "api": 1,
  "id": "req-8",
  "action": "type_text",
  "payload": {
    "testId": "ui.text_input",
    "text": "Hello World",
    "delayPerCharMs": 30
  }
}
```

### 8. `key_press`
Simulates pressing a specific key (e.g. Backspace, Return, Tab, Escape).
```json
{
  "api": 1,
  "id": "req-9",
  "action": "key_press",
  "payload": {
    "key": "Backspace",
    "count": 5
  }
}
```
