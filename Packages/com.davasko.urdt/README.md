# DavASko URDT (`com.davasko.urdt`)

Unity Remote Debugging Transport: a WebSocket server inside the game plus honest device-level input (Input System /
EventSystem), beacons with live game state, capture, audio tap and layout/coverage inspectors — the game side of the
URDT L3 testing agent.

**Quick start**
1. Add to `Packages/manifest.json`:
   `"com.davasko.urdt": "https://github.com/gDavASko/URDT.git?path=/Packages/com.davasko.urdt#urdt-l3-universal-player"`
2. Use the Input System (Active Input Handling = Input System Package or Both).
3. Enter Play Mode — the server auto-starts on `ws://127.0.0.1:7777/` (token `urdt-local`; override with
   `-urdtPort/-urdtToken` or `URDT_PORT/URDT_TOKEN`; disable with `URDT_AUTOSTART=0`; development builds need `-urdt`,
   release builds never start it).
4. Connect the agent (CoreAgent, MCP server `urdt`) and run tasks.

Full guide for AI agents (setup, beacons, tasks, knowledge, troubleshooting): `Docs/URDT_AI_Guide.md` in the URDT
repository. What the agent learns about a game is stored in `<project>/URDT_Knowledge/`.
