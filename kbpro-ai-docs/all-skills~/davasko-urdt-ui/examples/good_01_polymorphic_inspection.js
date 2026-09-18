/**
 * ✅ GOOD EXAMPLE 01: Polymorphic Target Inspection
 * 
 * WHY THIS IS CORRECT:
 * 1. Inspects the dedicated leaf component (`UrdtUiButtonTarget`, `UrdtUiToggleTarget`, etc.)
 *    instead of a monolithic grab-bag.
 * 2. Directly accesses type-specific fields (`ToggleValue`, `SliderValue`, `InputValue`)
 *    without guessing or checking for null on unrelated controls.
 */

function getTargetSlice(snapshot) {
    if (!snapshot || !snapshot.data || !snapshot.data.components) return null;
    const comps = snapshot.data.components;
    for (const key of Object.keys(comps)) {
        if (comps[key] && (comps[key].TargetId || comps[key].ScreenRect)) {
            return comps[key];
        }
    }
    return null;
}

async function inspectPolymorphicTargets(client) {
    // 1. Inspect a Button
    const btnRes = await client.call('inspect', { testId: 'btn_open_ui_suite' });
    const btn = btnRes.data.components.UrdtUiButtonTarget;
    console.log(`Button [${btn.TargetId}] interactable: ${btn.IsInteractable}, clicks: ${btn.InteractionCount}`);

    // 2. Inspect a Toggle
    const toggleRes = await client.call('inspect', { testId: 'ui.state_toggle' });
    const toggle = toggleRes.data.components.UrdtUiToggleTarget;
    console.log(`Toggle [${toggle.TargetId}] isOn: ${toggle.ToggleValue}`);

    // 3. Inspect a Slider
    const sliderRes = await client.call('inspect', { testId: 'ui.value_slider' });
    const slider = sliderRes.data.components.UrdtUiSliderTarget;
    console.log(`Slider [${slider.TargetId}] value: ${slider.SliderValue}`);
}
