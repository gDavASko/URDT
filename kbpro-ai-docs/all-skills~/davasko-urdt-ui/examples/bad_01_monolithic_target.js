/**
 * ❌ BAD EXAMPLE 01: Monolithic Target Anti-Pattern ("Мусор в маркерах")
 * 
 * WHY THIS IS WRONG:
 * 1. Monolithic components try to be Button, Toggle, Slider, InputField, Dropdown,
 *    and ScrollRect all at the same time in ONE class.
 * 2. In the Unity Inspector, every single element shows 6-8 empty "None" fields,
 *    causing visual pollution and wasting serialization overhead.
 * 3. Violates the Single Responsibility Principle (SRP).
 * 4. Leads to brittle null-checking spaghetti code when querying state.
 */

// ❌ ANTI-PATTERN: Trying to read toggle state from a generic monolithic component
async function badReadState(client, testId) {
    const res = await client.call('inspect', { testId });
    
    // ❌ WRONG: Monolithic component has every field, so developer must guess
    // which field actually belongs to this element!
    const target = res.data.components.MonolithicDebugTarget;
    
    if (target.Toggle != null) {
        console.log('Toggle:', target.ToggleValue);
    } else if (target.Slider != null) {
        console.log('Slider:', target.SliderValue);
    } else if (target.Selectable != null) {
        console.log('Button clicked:', target.InteractionCount);
    }
}
