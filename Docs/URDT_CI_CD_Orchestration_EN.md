# URDT CI/CD & Headless Orchestration Specification
*(Batch Execution, Memory Recycling, Parallel Worker Isolation & Crash Watchdogs)*

---

## Document Context and System Relationships

This document is the authoritative engineering specification for **Autonomous CI/CD and Headless Orchestration** for the URDT Reviewer. It forms part of the modular documentation suite:

1. **[Core Architecture & Master Overview](URDT_Autonomous_Reviewer_Architecture_EN.md)** — Architectural vision, speed pyramid, Hexagonal core, and master subsystem map.
2. **[Preparation & Analysis Subsystem (Mode 1)](URDT_Autonomous_Reviewer_Preparation_Analysis_EN.md)** — Pre-flight audit, scaffolding, and topological mapping.
3. **[Game & Testing Subsystem (Mode 2)](URDT_Autonomous_Reviewer_Game_Testing_EN.md)** — Live gameplay execution, multi-touch kinematics, and tactical dispatching.
4. **[Wire Protocol v2 Specification](URDT_Wire_Protocol_Specification_EN.md)** — Low-level networking, binary frame layouts, JSON envelopes, and C# structs.
5. **[CI/CD & Headless Orchestration (This Document)](URDT_CI_CD_Orchestration_EN.md)** — Headless execution, port isolation, and crash watchdogs.
6. **[Failure Evidence Contract](URDT_Failure_Evidence_Contract_EN.md)** — Schema for defect packages, crash dashcam, and self-healing.

---

## 1. Overview of Headless CI/CD Execution

The autonomous reviewer is engineered to operate in unattended nightly builds and regression pipelines (GitLab CI, GitHub Actions, TeamCity) without human intervention:

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                    CI PIPELINE RUNNER (Node.js CoreAgent)                   │
│   Command: npm run start -- --ci --max-duration 600 --report-out ...       │
└──────────────────────────────────────┬──────────────────────────────────────┘
                                       │ 1. Spawn with -urdtPort 0
                                       ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                   HEADLESS UNITY RUNNER (Linux Xvfb / Win)                  │
│   Unity.exe -batchmode -force-vulkan -screen-width 1920 -screen-height 1080 │
│             -executeMethod UrdtTestRunner.RunHeadless ...                   │
└──────────────────────────────────────┬──────────────────────────────────────┘
                                       │ 2. Writes /tmp/urdt_handshake_*.json
                                       ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                DYNAMIC ZERO-COLLISION BINDING & HANDSHAKE                   │
│   - Ephemeral TCP Port (e.g. 54321)                                         │
│   - Unique Named Pipe (\\.\pipe\urdt_fast_ticker_<Guid>)                    │
└──────────────────────────────────────┬──────────────────────────────────────┘
                                       │ 3. Execute L3/L2/L1 Scenarios
                                       ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                       SHUTDOWN / RECYCLE ARBITRATION                        │
│   - Invariants satisfied:                  Exit Code 0                      │
│   - Regressions / Crashes detected:        Exit Code 1                      │
│   - Mono Heap Fragmentation (20 mechanics):Exit Code 42 (RECYCLE_REQUESTED) │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

## 2. Command Line Interface & Launch Modes

### 2.1. CLI Orchestration Arguments
The CI pipeline spawns Unity via command line with virtual display rendering enabled:

```bash
# Linux CI Runner (with virtual X server and software Vulkan/OpenGL acceleration):
xvfb-run --auto-servernum --server-args="-screen 0 1920x1080x24" \
  ./Unity -batchmode -force-vulkan -screen-width 1920 -screen-height 1080 -screen-fullscreen 0 \
  -projectPath "/workspace" -executeMethod UrdtTestRunner.RunHeadless \
  -urdtPort 0 -urdtHandshakeFile "/tmp/urdt_handshake_${CI_JOB_ID}.json" \
  -urdtToken "${CI_JOB_TOKEN}" -maxDuration 600

# Windows CI Runner:
Unity.exe -batchmode -force-vulkan -screen-width 1920 -screen-height 1080 -screen-fullscreen 0 \
  -projectPath "E:\Projects\URDT" -executeMethod UrdtTestRunner.RunHeadless \
  -urdtPort 0 -urdtHandshakeFile "C:\temp\urdt_handshake_%CI_JOB_ID%.json" \
  -urdtToken "%CI_JOB_TOKEN%" -maxDuration 600
```

---

## 3. C# Test Runner Implementation (`UrdtTestRunner.cs`)

```csharp
using System;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace URDT.Runtime.Runner
{
    public static class UrdtTestRunner
    {
        public static void RunHeadless()
        {
            string[] args = Environment.GetCommandLineArgs();
            int port = 9002;
            string token = Guid.NewGuid().ToString("N");
            string handshakePath = null;
            int maxDurationSec = 600;

            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "-urdtPort" && i + 1 < args.Length) int.TryParse(args[i + 1], out port);
                if (args[i] == "-urdtToken" && i + 1 < args.Length) token = args[i + 1];
                if (args[i] == "-urdtHandshakeFile" && i + 1 < args.Length) handshakePath = args[i + 1];
                if (args[i] == "-maxDuration" && i + 1 < args.Length) int.TryParse(args[i + 1], out maxDurationSec);
            }

            Debug.Log($"[URDT] Starting Headless Test Runner on port {port} (Timeout: {maxDurationSec}s)...");
            var bootstrap = UrdtRuntimeBootstrap.EnsureInitialized(port, token);
            
            if (!string.IsNullOrEmpty(handshakePath))
            {
                var info = new UrdtHandshakeInfo
                {
                    port = bootstrap.BoundPort,
                    pipeName = bootstrap.BoundPipeName,
                    token = token,
                    pid = System.Diagnostics.Process.GetCurrentProcess().Id,
                    status = "READY"
                };
                System.IO.File.WriteAllText(handshakePath, JsonUtility.ToJson(info));
            }

            // Subscribe to audit completion
            bootstrap.OnAuditFinished += (exitCode) =>
            {
                Debug.Log($"[URDT] Audit finished with exit code: {exitCode}");
#if UNITY_EDITOR
                EditorApplication.Exit(exitCode);
#else
                Application.Quit(exitCode);
#endif
            };
        }
    }

    [Serializable]
    public struct UrdtHandshakeInfo
    {
        public int port;
        public string pipeName;
        public string token;
        public int pid;
        public string status;
    }
}
```

---

## 4. Handshake Probe Synchronization

- Upon launching `Unity.exe`, shader warm-up, asset database initialization, and scene loading can consume 10–60 seconds.
- `CoreAgent` does not prematurely fail; it initiates a **Handshake Polling Probe**:
  - Polls for existence and valid JSON in `-urdtHandshakeFile` every 1000 ms using exponential backoff up to a 60-second ceiling.
  - Extracts the dynamically assigned `port`, `pipeName`, and `token`.
  - Establishes connection and deletes the handshake file to maintain workspace cleanliness.

---

## 5. Two-Tier CI Memory Recycling Protocol

Due to the internal architecture of the Unity Mono Virtual Machine (Boehm-Demers-Weiser Garbage Collector), allocated heap pages are **never returned to the host OS**. Over extended continuous testing runs across 20+ mechanics, Mono Heap fragmentation leads to inevitable process bloat and OOM termination.

The reviewer implements a **Two-Tier Hygiene Protocol**:

### 5.1. Tier 1: Inter-Test Asset Cleanup
Following the completion of each individual test suite or mechanic:
```csharp
Resources.UnloadUnusedAssets();
GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true, true);
```
Frees transient textures, audio clips, and unreferenced meshes.

### 5.2. Tier 2: Clean Process Recycle Every 20 Mechanics (`exit 42`)
1. Every 20 completed mechanics, `CoreAgent` serializes accumulated testing progress and topological multigraph into `test_progress.json`.
2. Sends the command `CMD_REQUEST_RECYCLE` to Unity.
3. Unity flushes log buffers and terminates with exit code **`exit 42 (RECYCLE_REQUESTED)`**.
4. The `CoreAgent` runner recognizes exit code 42 as a normal lifecycle event, spawns a clean cold instance of `Unity.exe`, executes the handshake, and resumes testing seamlessly from mechanic 21.

---

## 6. Multi-Worker Parallel Isolation Protocol

Running concurrent test jobs (e.g. 4 parallel workers on a 16-vCPU CI host) using hardcoded port 9002 or static pipe names causes fatal `EADDRINUSE` collisions and command cross-talk.

**The Zero-Collision Binding Protocol:**
1. Each worker is spawned with unique job parameters:
   `-urdtPort 0 -urdtHandshakeFile "/tmp/urdt_handshake_${CI_JOB_ID}_${workerId}.json"`
2. Passing `-urdtPort 0` prompts the underlying OS socket library (`TcpListener(IPAddress.Loopback, 0)`) to bind to a guaranteed free ephemeral port (range 49152–65535).
3. The server generates a unique Named Pipe using a cryptographic GUID:
   `\\.\pipe\urdt_fast_ticker_{Guid.NewGuid():N}`
4. Metadata is written atomically to the worker handshake file.
5. The corresponding `CoreAgent` instance connects strictly to its assigned port and pipe, guaranteeing total process isolation.

---

## 7. Headless Virtual Rendering in Linux Docker (SwiftShader / Mesa)

### 7.1. The `-nographics` Dilemma
In headless CI environments without physical GPUs, launching Unity with `-nographics` disables the graphical subsystem:
- Canvas batching is bypassed; `Canvas.willRenderCanvases` is never invoked.
- `CanvasScaler` fails to compute screen scaling matrices.
- `GraphicRaycaster.Raycast` returns empty hit collections.
- The AI reviewer becomes completely blind to buttons, sliders, and interactive UI elements.

### 7.2. Resolution via Xvfb and Software Rasterization
Unity is executed inside an X-Virtual Framebuffer with software Vulkan/OpenGL acceleration:
```bash
xvfb-run --auto-servernum --server-args="-screen 0 1920x1080x24" \
  ./Unity -batchmode -force-vulkan -screen-width 1920 -screen-height 1080 -screen-fullscreen 0 \
  -projectPath "/workspace" -executeMethod UrdtTestRunner.RunHeadless ...
```
This forces Unity to construct the full rendering graph, enabling authentic `CanvasScaler` computations and `GraphicRaycaster` hitboxes.

---

## 8. Post-Mortem Native Crash Watchdog

### 8.1. Native Crash Limitations
Native C++ crashes (Access Violation `0xC0000005` in PhysX/IL2CPP, `SIGSEGV` / `SIGBUS` on Linux, native C++ OOM, GPU driver resets) kill the engine process instantly at the OS level without invoking C# exception handlers (`Application.logMessageReceived`).

### 8.2. Autopsy Protocol
```
                   [ Unity Process Terminates Abnormaly ]
                                      │
                                      ▼
                   [ CoreAgent intercepts exit(code != 0) ]
                                      │
                                      ▼
                   [ Locate Player.log across standard paths ]
                   - Linux: ~/.config/unity3d/<Company>/<Product>/Player.log
                   - Windows: %USERPROFILE%\AppData\LocalLow\...\Player.log
                                      │
                                      ▼
                   [ Extract Trailing 200 Lines of Log ]
                   Isolate:
                   - Signal Name: SIGSEGV / 0xC0000005
                   - Faulting Library: UnityPlayer.dll / libil2cpp.so
                   - Stack Trace: Native call chain
                                      │
                                      ▼
                   [ Create NATIVE_ENGINE_CRASH Card in Report ]
                                      │
                                      ▼
                   [ Exit Pipeline with Exit Code 1 ]
```

---

## 9. IL2CPP Type Preservation Rules (`link.xml`)

To guarantee runtime reflection stability in release builds with IL2CPP Bytecode Strip Code enabled, `Packages/com.davasko.urdt/link.xml` is maintained:

```xml
<linker>
  <!-- Must match the Unity asmdef name used by this package. -->
  <assembly fullname="KBP.URDT" preserve="all"/>
  <assembly fullname="UnityEngine.UI" preserve="all">
    <type fullname="UnityEngine.UI.GraphicRaycaster" preserve="all"/>
    <type fullname="UnityEngine.UI.CanvasScaler" preserve="all"/>
    <type fullname="UnityEngine.EventSystems.PointerEventData" preserve="all"/>
  </assembly>
  <assembly fullname="Unity.InputSystem" preserve="all">
    <type fullname="UnityEngine.InputSystem.Touchscreen" preserve="all"/>
    <type fullname="UnityEngine.InputSystem.LowLevel.TouchState" preserve="all"/>
  </assembly>
</linker>
```

---

## 10. Process Exit Codes & Graceful Shutdown

| Exit Code | Classification | Meaning & Pipeline Action |
| :--- | :--- | :--- |
| **`0`** | **`SUCCESS`** | All declared GDD invariants were exercised under the coverage budget with zero critical defects. |
| **`1`** | **`FAILURE`** | Functional crash, unhandled NRE, critical soft-lock, or GDD invariant violation detected. |
| **`42`**| **`RECYCLE_REQUESTED`** | Normal Mono Heap hygiene lifecycle event; runner spawns fresh cold Unity instance and resumes. |

### Graceful Shutdown on Cancellation (`SIGTERM`)
Upon receiving runner cancellation or job timeout:
1. Immediately dispatches `EMERGENCY_RELEASE_ALL_POINTERS` to Unity.
2. Flushes accumulated telemetry, topological graph, and defect records into `Docs/QA_Audit_Report.html`.
3. Terminates Unity process cleanly via OS signals.
