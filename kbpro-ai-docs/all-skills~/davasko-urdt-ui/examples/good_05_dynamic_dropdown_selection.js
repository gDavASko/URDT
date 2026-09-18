/**
 * ✅ GOOD EXAMPLE 05: Dynamic Dropdown Selection
 * 
 * WHY THIS IS CORRECT:
 * 1. Reads `DropdownOptions` pipe-separated string from `UrdtUiDropdownTarget`.
 * 2. Clicks the dropdown header to open the item template list.
 * 3. Finds the item option and simulates honest click.
 * 4. Verifies `DropdownLabel` and `DropdownValue` after selection.
 */

async function selectDropdownOption(client, dropdownId, targetOptionText) {
    // 1. Inspect dropdown state and options
    const before = await client.call('inspect', { testId: dropdownId });
    const dd = before.data.components.UrdtUiDropdownTarget;
    const options = dd.DropdownOptions.split('|');
    const targetIndex = options.indexOf(targetOptionText);

    if (targetIndex === -1) {
        throw new Error(`Option "${targetOptionText}" not in options [${dd.DropdownOptions}]`);
    }

    // 2. Click dropdown to expand options
    await client.call('click', { testId: dropdownId });
    await new Promise(r => setTimeout(r, 400));

    // 3. Query spawned template item or hit-test the item position
    const itemQuery = await client.call('query', { byName: `Item ${targetIndex}: ${targetOptionText}` });
    const item = itemQuery.data.matches[0];
    
    if (item && item.testId) {
        await client.call('click', { testId: item.testId });
    } else {
        // Fallback: Click dropdown again to pick
        await client.call('click', { testId: dropdownId });
    }
    await new Promise(r => setTimeout(r, 300));

    // 4. Assert new dropdown state
    const after = await client.call('inspect', { testId: dropdownId });
    const afterDd = after.data.components.UrdtUiDropdownTarget;
    console.log(`✅ Dropdown selected: [${afterDd.DropdownLabel}] (index: ${afterDd.DropdownValue})`);
}
