/**
 * ✅ GOOD EXAMPLE 04: Typed Text Injection & Keyboard Input
 * 
 * WHY THIS IS CORRECT:
 * 1. Simulates physical click to give the input field EventSystem focus.
 * 2. Uses URDT `type_text` to send character keys through InputSystem.
 * 3. Uses `key_press` for editing (Backspace/Enter).
 * 4. Verifies `InputValue` on the dedicated `UrdtUiInputTarget`.
 */

async function testTextInput(client, inputId, textToType) {
    // 1. Click to gain focus
    await client.call('click', { testId: inputId });
    await new Promise(r => setTimeout(r, 200));

    // 2. Type text character by character
    await client.call('type_text', {
        testId: inputId,
        text: textToType,
        delayPerCharMs: 25
    });
    await new Promise(r => setTimeout(r, 300));

    // 3. Assert input value matches
    const snapshot = await client.call('inspect', { testId: inputId });
    const inputComp = snapshot.data.components.UrdtUiInputTarget;
    if (inputComp.InputValue !== textToType) {
        throw new Error(`Expected "${textToType}" but got "${inputComp.InputValue}"`);
    }

    console.log(`✅ Text typed successfully: "${inputComp.InputValue}"`);

    // 4. Backspace clearance
    await client.call('key_press', { key: 'Backspace', count: textToType.length });
    await new Promise(r => setTimeout(r, 200));

    const cleared = await client.call('inspect', { testId: inputId });
    if (cleared.data.components.UrdtUiInputTarget.InputValue !== '') {
        throw new Error('Failed to clear input field!');
    }
    console.log('✅ Input field cleared cleanly');
}
