# Module creation pipeline (Pipeline Order)

Below is the strict sequence of steps and calls to specialized AI skills for assembling a turnkey gameplay module.

| # | Development stage | Skill used | Input data | Stage output / Deliverable |
|---|---|---|---|---|
| **0** | **Path investigation and planning** | `kbpro-code-navigator` + `kbpro-module-creator` | Spec (ТЗ) from the user | `Module Creation Plan` drafted and approved |
| **1** | **Spec analysis and decomposition** | `kbpro-code-navigator` + `kbpro-module-creator` | Spec + Code analogues | Systems and components identified by SOLID / SRP |
| **2** | **I/O DTOs (Module I/O)** | `kbpro-module-input-builder` + `output-builder` | Plan specification | Classes `MyModuleInput.cs` and `MyModuleOutput.cs` |
| **3** | **View development (GameComponent)** | `kbpro-module-components-builder` | Spec for visuals and input | C# classes `*Component.cs` (no business logic) |
| **4** | **Business logic development** | `kbpro-module-systems-builder` | Logic specification | Clean C# systems `*System.cs` (`[SerializeReference]`) |
| **5** | **Orchestrator (AbstractGameModule)** | `kbpro-node-module-builder` | Module contract | Module C# class `*Module.cs` with I/O binding |
| **6** | **Prefab assembly in the Unity Editor** | `unity-prefab-builder` (via the UnitySkills REST API or Auto-Builder) | C# code + prefab tree | Configured `*Module.prefab` with DI injections |
| **7** | **Registration in the scenario (V2)** | `kbpro-scenario-node-builder` + `scenario-link-builder` | Prefab + ScenarioGraph | Module node integrated into the ScenarioGraph |
| **8** | **Registration in configs (V1)** | `kbpro-module-creator` | Prefab + Settings | ID entry in `SOConstantsContainer` and `SOGameModuleSettings` |
| **9** | **Adding sounds (Audio)** | `kbpro-audio-builder` | Audio spec | Integration of `ISoundSystem` / `ISoundAccessor` |
| **10** | **Tutor integration (Tutors)** | `kbpro-tutor-builder` | Tutor spec | Tutor logic with `EventStartTutor` and ghost hand |
| **11** | **Analytics collection (Analytics)** | `kbpro-analytics-builder` | Analytics map | Calls to `LazySrv<IStatisticService>` |
| **12** | **Validation and build** | `unity-playmode-validation` | The full created module | No compilation errors, successful PlayModule test |
| **13** | **Module documentation** | `kbpro-module-describer` | Source code + Scene | Ready module description `*_reference.md` in `ModuleExamples/` |

## Rules for moving between steps

1. **Do not skip ahead:** Every step must be completed and tested before moving to the next.
2. **Use the specialized tool:** Do not write component code in the router — call `kbpro-module-components-builder`, etc.
3. **Confirmation:** If you find ambiguities in the spec or details at the boundary between steps (e.g. an audio clip is missing from the constants), ask the user a clarifying question.
