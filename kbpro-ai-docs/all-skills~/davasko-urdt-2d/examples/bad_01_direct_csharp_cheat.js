/**
 * ANTI-PATTERN: Direct C# Internal Cheating
 * 
 * WHY THIS IS FORBIDDEN:
 * Modifying internal serialized fields or invoking C# win events directly bypasses
 * physics, EventSystem, input colliders, and UI raycasters. This invalidates
 * the test and breaks realistic human/device interaction validation.
 */

// ❌ FORBIDDEN: Direct C# injection / cheat calls
async function cheatPassMechanic(client, moduleTargetId) {
    // ILLEGAL: Attempting to invoke C# reflection or internal methods
    /*
    await client.call('eval_csharp', {
        code: `
            var mod = GameObject.Find("${moduleTargetId}").GetComponent<BaseMechanic2DModule>();
            mod.OnCompleted?.Invoke(mod); // FORBIDDEN CHEAT!
        `
    });
    */
    throw new Error('Direct C# state mutation is strictly forbidden under URDT tenets.');
}
