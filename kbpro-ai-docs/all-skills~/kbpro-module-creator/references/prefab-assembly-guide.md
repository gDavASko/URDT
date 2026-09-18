# Prefab Assembly Guide (Unity)

After writing the C# code, the module must be physically assembled as a prefab in the Unity Editor, the DI links between systems and components must be configured, and the click raycast physics must be set up.

## 1. Required prefab hierarchy

```
[ModuleName]Module (Root GameObject)      ← module script attached (subclass of AbstractGameModule)
├── VisualGroup (GameObject)               ← visual group
│   └── ToolObject (GameObject)            ← object with a component (e.g. DrillToolComponent)
├── Canvas [UI HUD] (GameObject)           ← module's local HUD
└── [AudioGroup] (GameObject)              ← local audio sources (if needed)
```

## 2. Configuring the module inspector (Root GameObject)

The DI links are configured on the module's root object:
1. **The `_systems` array:**
   - Click **+** in the module's `_systems` array.
   - Select your system types from the dropdown (Managed Reference Picker) (`DrillPhysicsSystem`, `WashSoundsSystem`, etc.).
   - Fill in the systems' serialized parameters (distances, speeds, etc.).
2. **The `_componentProviders` array:**
   - Every component referenced by `[InjectComponent]` in the systems needs a provider.
   - Click **+** in the `_componentProviders` array on the Root.
   - Select `LinkedComponentProvider`.
   - Drag the GameObject with the component from the prefab hierarchy into the provider's reference field.

## 3. Physics and layers (Physics & Layers)

- All objects that the player must click or drag (with input components) must have a physics collider (e.g. `BoxCollider2D` with `Is Trigger` checked).
- **Critically important:** assign these objects the correct physics Layer configured in the project for click raycasts (e.g. the `Clickable` or `UI` layer, depending on the input camera settings). Without this, the player's click rays will not reach the colliders.

## 4. Assembly automation methods

The AI agent uses one of two ways to configure the prefab:

### Method A: Via the UnitySkills plugin (Preferred)
If the `Packages/com.besty.unity-skills/` folder is present in the project, the AI developer uses the automation REST server (typically `http://127.0.0.1:8090`) to create GameObjects, attach components, configure serialized fields, and save the prefab.

### Method B: Via an Auto-Builder script (Fallback)
If the plugin is not present, the AI generates a temporary C# auto-assembly script in the `Assets/Editor/` folder (e.g. `Assets/Editor/[ModuleName]AutoBuilder.cs`).
**Mandatory condition:** at the end of the assembly method, the script must contain self-deletion logic:
```csharp
FileUtil.DeleteFileOrDirectory("Assets/Editor/[ModuleName]AutoBuilder.cs");
AssetDatabase.Refresh();
```
An example of the auto-builder code is given in [module-code-examples.md](file://../examples/module-code-examples.md).
