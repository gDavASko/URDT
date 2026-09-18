# Registering module settings (SOGameModuleSettings)

This reference describes the process of registering a game module in the global ScriptableObject configuration and launching it from C# code.

---

## 1. The role of SOGameModuleSettings

**SOGameModuleSettings** is the central ScriptableObject that stores the sequences and configurations of all the project's game modules (scenarios).

- **Path in the project**: `Assets/KBPro/kbpro-modules/Runtime/GameModuleSystem/SOGameModuleSettings.asset`
- Each entry in the `_scripts` array is a `GameModuleSetup`, which contains a scenario identifier (`Id`), the final screen transition (`FinalTransitionId`), and an array of steps (`Modules`).

---

## 2. Step-by-step module registration

### Step 1: Add the module ID to SOConstantsContainer

Before configuring in the inspector, the module needs a string identifier that will be used in code and graphs.

1. Find the `SOConstantsContainer` asset in the Project window.
2. In the groups section, find the **Modules** group (or create it if it doesn't exist).
3. Add a new row with your module's name in upper case (e.g. `TRAIN_CLEANING`).
4. Click the **Generate Code** button in the asset inspector.
5. This will automatically update the static constants class `Constants.cs`:
   ```csharp
   public static class MODULES
   {
       public const string TRAIN_CLEANING = "TRAIN_CLEANING";
   }
   ```

### Step 2: Create a GameModuleSetup in the settings

1. Select the `SOGameModuleSettings` asset.
2. In the **Scripts** array, click the **+** button (add element).
3. In the **Id** field that appears, select your generated ID (`TRAIN_CLEANING`) from the dropdown (it uses `ConstSelector` based on `Constants.MODULES`).
4. Set the **FinalTransitionId** (e.g. `FADE`), which determines the type of visual transition at the end of the whole scenario.

### Step 3: Add a step (Module Setup)

Each scenario entry contains a `Modules` array whose elements implement the `IGameModuleSetup` interface.

1. In the **Modules** array of the script being created, click **+**.
2. Choose the step type **`GameSimpleModuleAssetSetup`** (the base option for linear modules).
3. Fill in the fields:
   - **moduleRef**: Drag your module's prefab. It's important that the prefab is added to the **Addressables** system (in the `Modules` group), and that its address matches the prefab's name.
   - **endModuleTransitionId**: The visual transition when the module closes (e.g. `FADE_OUT`).
   - **gameWindowId**: The identifier of the HUD UI window that automatically opens when the module starts (e.g. `GAME`).

Schematic view of the structure in the inspector:
```yaml
GameModuleSetup:
  Id: "TRAIN_CLEANING"
  FinalTransitionId: "FADE"
  Modules:
    - [GameSimpleModuleAssetSetup]
        moduleRef: [Addressable Asset: TrainCleaningModule.prefab]
        endModuleTransitionId: "FADE_OUT"
        gameWindowId: "GAME"
```

---

## 3. Launching a module from C# code

To launch a registered module, use the `IsolatedGameModuleService` service. Access it via `LazySrv<T>` to follow DI principles:

```csharp
using KBP.CORE;
using UnityEngine;

namespace KBP.RAILWAY_COW.HUD
{
    public class HUDPresenter : MonoBehaviour
    {
        // Dependency injection without direct access to a singleton
        private LazySrv<IsolatedGameModuleService> _moduleService = new LazySrv<IsolatedGameModuleService>();

        public void OnStartCleaningButtonClick()
        {
            // Launch the module by its constant ID
            _moduleService.Value.Run(Constants.MODULES.TRAIN_CLEANING);
        }

        private void OnDestroy()
        {
            // Mandatory release of LazySrv
            _moduleService.Dispose();
        }
    }
}
```
> [!IMPORTANT]
> The `Run()` method will load the prefab from Addressables, instantiate it in the scene, call `Initialize()`, then `Startable()`, open the specified `gameWindowId`, and hand control over to the module's logic systems.
