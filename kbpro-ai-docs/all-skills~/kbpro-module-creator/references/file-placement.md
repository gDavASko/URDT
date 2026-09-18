# File and asset placement rules (File Placement Rules)

The AI developer must adapt to the structure of the specific Unity project. Creating folders and files "blindly" based on abstract examples is forbidden.

## 1. Investigating the project structure (Mandatory step)

Before creating any files, do a thorough search of the repository:
1. **Find existing modules:** Find C# classes that inherit from `AbstractGameModule`.
2. **Analyze directories:** Study the file paths of these classes, their prefabs, ScenarioGraph assets, and ScriptableObject settings files.
3. **Identify the pattern:** Place all new files strictly by analogy with the structure found.

## 2. Specific paths for Dentistry-cow

The **Dentistry-cow** project uses the following hierarchy:
- **C# scripts (module, component, and system classes):**
  `Assets/Dentistry-cow/Scripts/Modules/[ModuleName]/`
- **Module prefabs:**
  `Assets/Dentistry-cow/Prefabs/Modules/[ModuleName]/[ModuleName].prefab`
- **Scenarios (ScenarioGraph SO assets):**
  `Assets/Configs/SO/Modules/`
- **Global module settings:**
  `Assets/Configs/SO/SOGameModuleSettings.asset`

## 3. Default hierarchy (when no structure exists)

If the project is completely new, empty, or doesn't have game module folders yet, propose the following default structure to the user:
- **C# scripts:** `Assets/[ProjectName]/Scripts/Modules/[ModuleName]/`
- **Prefabs:** `Assets/[ProjectName]/Prefabs/Modules/[ModuleName]/[ModuleName].prefab`
- **Scenarios:** `Assets/Configs/SO/Modules/[ModuleName]_ScenarioGraph.asset`
- **Global settings:** `Assets/Configs/SO/SOGameModuleSettings.asset`

## 4. Agreeing on paths

All paths for the files being created (scripts, prefabs, and assets) must be explicitly listed in Section 1 of your `Module Creation Plan` and agreed with the user before starting to write C# code.
