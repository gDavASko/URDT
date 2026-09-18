/**
 * ❌ BAD EXAMPLE 03: Direct C# Callback Invocation Anti-Pattern
 * 
 * WHY THIS IS WRONG:
 * 1. Invoking `button.onClick.Invoke()` or calling C# methods directly in the Editor
 *    completely bypasses the Unity InputSystem, EventSystem, GraphicRaycaster,
 *    and canvas interactability checks.
 * 2. It gives a FALSE POSITIVE: a button might be hidden behind a dialog, invisible,
 *    or non-interactable, yet direct invocation will still succeed!
 * 3. URDT requires honest device-level input: real simulated clicks through InputSystem.
 */

// ❌ ANTI-PATTERN: Executing C# eval to invoke onClick directly
async function badDirectInvoke(unityCli, buttonName) {
    // ❌ WRONG: Calling onClick directly from outside!
    await unityCli.execute('eval', {
        code: `GameObject.Find("${buttonName}").GetComponent<Button>().onClick.Invoke();`
    });
}
