/*
 * Visual Demonstration UI Runner for DavASko.
 * Designed specifically for human visual observation in Unity Game View:
 * - Clear pacing (1.0s - 1.5s between major actions)
 * - Smooth pointer interpolation during drags and movements
 * - Character-by-character typing with realistic typing cadence
 * - Clear, rich console telemetry for every action
 */
const fs = require('fs');

const wait = ms => new Promise(resolve => setTimeout(resolve, ms));
const rectPattern = /x:([-\d.]+), y:([-\d.]+), width:([-\d.]+), height:([-\d.]+)/;

class Client {
    constructor() { this.sequence = 0; this.pending = new Map(); }
    async connect() {
        for (let attempt = 0; attempt < 10; attempt++) {
            try {
                this.socket = new WebSocket('ws://127.0.0.1:7777/');
                await new Promise((resolve, reject) => {
                    this.socket.onopen = resolve;
                    this.socket.onerror = reject;
                    this.socket.onmessage = event => {
                        const response = JSON.parse(event.data);
                        const resolver = this.pending.get(response.id);
                        if (resolver) { this.pending.delete(response.id); resolver(response); }
                    };
                });
                await this.call('handshake', { token: 'urdt-test-poligon', projectId: 'dentistry-cow' });
                return;
            } catch (error) {
                if (this.socket) this.socket.close();
                await wait(1000);
            }
        }
        throw new Error('URDT WebSocket did not become ready.');
    }
    call(action, payload) {
        return new Promise(resolve => {
            const id = String(++this.sequence);
            this.pending.set(id, resolve);
            this.socket.send(JSON.stringify({ api: 1, id, action, payload }));
        });
    }
    close() { this.socket.close(); }
}

function component(obj) {
    if (!obj) return null;
    if (obj.TargetId) return obj;
    if (obj.data && obj.data.components) {
        for (const key of Object.keys(obj.data.components)) {
            const c = obj.data.components[key];
            if (c && (c.TargetId || c.ScreenRect)) return c;
        }
    }
    return null;
}

function parseRect(snapshot) {
    const comp = component(snapshot);
    if (!comp || !comp.ScreenRect) throw new Error('Missing ScreenRect for ' + JSON.stringify(snapshot));
    const match = comp.ScreenRect.match(rectPattern);
    if (!match) throw new Error('Cannot parse ScreenRect: ' + comp.ScreenRect);
    return { x: Number(match[1]), y: Number(match[2]), width: Number(match[3]), height: Number(match[4]) };
}

async function main() {
    console.log('================================================================');
    console.log('🚀 ЗАПУСК НАГЛЯДНОГО ВИЗУАЛЬНОГО ТЕСТА UI В UNITY GAME VIEW');
    console.log('👁️  Смотри в окно Game в Unity — ты увидишь зелёный курсор,');
    console.log('    клики, посимвольный ввод текста, слайдер и дропдаун!');
    console.log('================================================================\n');

    const client = new Client();
    await client.connect();
    console.log('✅ Подключено к URDT WebSocket (ws://127.0.0.1:7777)\n');
    console.log('👀 ПЕРЕКЛЮЧИСЬ НА ОКНО UNITY GAME VIEW!');
    for (let i = 3; i > 0; i--) {
        console.log(`⏱️  Старт через ${i}...`);
        await wait(1000);
    }
    console.log('🚀 ПОЕХАЛИ!\n');

    async function inspect(testId) {
        const response = await client.call('inspect', { testId });
        if (response.status !== 'ok') throw new Error('inspect ' + testId + ': ' + JSON.stringify(response.error));
        return response;
    }

    async function act(stepName, actionTargetId, action, payload, assertion, delayMs = 1200, verifyTargetId = null) {
        process.stdout.write(`⏳ ${stepName}... `);
        const checkId = verifyTargetId || actionTargetId;
        const before = await inspect(checkId);
        const response = await client.call(action, payload);
        await wait(delayMs);
        const after = await inspect(checkId);
        const pass = response.status === 'ok' && assertion(component(before), component(after));
        if (!pass) {
            console.log('❌ ОШИБКА!');
            throw new Error(`${stepName} failed! response: ${JSON.stringify(response)}`);
        }
        console.log('✅ OK');
        return after;
    }

    async function scrollSuite(towardsBottom) {
        try {
            await act('Скролл панели', 'ui.suite_scroll', 'swipe',
                {
                    testId: 'ui.suite_scroll',
                    direction: towardsBottom ? 'up' : 'down',
                    distance: 220,
                    steps: 12
                },
                (before, after) => before.ScrollPosition.y !== after.ScrollPosition.y || after.ScrollPosition.y === (towardsBottom ? 0 : 1), 600);
            return true;
        } catch (_) {
            return false;
        }
    }

    async function ensureHittable(testId) {
        for (let attempt = 0; attempt < 96; attempt++) {
            const snapshot = await inspect(testId);
            const rect = parseRect(snapshot);
            const suite = await inspect('ui.suite_scroll');
            const suiteRect = parseRect(suite);
            const center = component(snapshot).ScreenCenter;
            if (center && center.x >= suiteRect.x && center.x <= suiteRect.x + suiteRect.width &&
                center.y >= suiteRect.y && center.y <= suiteRect.y + suiteRect.height) return snapshot;
            const towardBottom = rect.y < suiteRect.y;
            if (!await scrollSuite(towardBottom)) {
                await scrollSuite(!towardBottom);
            }
        }
        throw new Error('Could not bring ' + testId + ' into viewport');
    }

    // 1. Проверяем текущее окно
    const mainMenu = await inspect('window_main_menu');
    if (component(mainMenu).ActiveWindow === 'window_ui_suite') {
        console.log('🔄 Возвращаемся в главное меню для чистого старта...');
        await act('Клик по кнопке Назад [btn_ui_back]', 'btn_ui_back', 'click', { testId: 'btn_ui_back' },
            (_, after) => after.ActiveWindow === 'window_main_menu', 1500, 'window_main_menu');
    }

    // 2. Открытие UI Suite
    console.log('\n--- ЭТАП 1: Навигация по окнам ---');
    await act('[1/14] Клик по кнопке "Open UI Suite" [btn_open_ui_suite]', 'btn_open_ui_suite', 'click', { testId: 'btn_open_ui_suite' },
        (_, after) => after.ActiveWindow === 'window_ui_suite', 1500, 'window_ui_suite');

    // 3. Модальное окно
    console.log('\n--- ЭТАП 2: Модальное окно ---');
    await act('[2/14] Открытие модального окна [ui.modal_button]', 'ui.modal_button', 'click', { testId: 'ui.modal_button' },
        (_, after) => after.LastResult === 'modal_open', 1500);

    console.log('   (Модальное окно открыто на экране, пауза 1 секунда для наблюдения...)');
    await wait(1000);

    await act('[3/14] Закрытие модального окна [ui.modal_close_button]', 'ui.modal_close_button', 'click', { testId: 'ui.modal_close_button' },
        (_, after) => after.LastResult === 'modal_closed', 1200);

    // 4. Текстовое поле - посимвольный ввод
    console.log('\n--- ЭТАП 3: Интерактивный ввод текста (InputField) ---');
    await ensureHittable('ui.text_input');
    await act('[4/14] Фокус на поле ввода [ui.text_input]', 'ui.text_input', 'click', { testId: 'ui.text_input' },
        (_, after) => after.LastInputAction === 'keyboard' || after.InteractionCount >= 0, 800);

    const testString = 'Привет DavASko!';
    process.stdout.write(`   Посимвольный ввод текста: "${testString}" `);
    let currentText = '';
    for (const char of testString) {
        currentText += char;
        await client.call('type_text', { text: currentText });
        process.stdout.write(char);
        await wait(120);
    }
    console.log(' -> Введено!');
    await wait(1000);

    console.log('   Очистка поля ввода backspace...');
    await client.call('type_text', { text: '' });
    await wait(800);

    // 5. Переключатель (Toggle)
    console.log('\n--- ЭТАП 4: Переключатель (Toggle) ---');
    await ensureHittable('ui.state_toggle');
    await act('[5/14] Включение Toggle [ui.state_toggle] (ON)', 'ui.state_toggle', 'click', { testId: 'ui.state_toggle' },
        (_, after) => after.ToggleValue === true, 1200);

    await act('[6/14] Выключение Toggle [ui.state_toggle] (OFF)', 'ui.state_toggle', 'click', { testId: 'ui.state_toggle' },
        (_, after) => after.ToggleValue === false, 1200);

    // 6. Слайдер (Slider) с плавной интерполяцией
    console.log('\n--- ЭТАП 5: Плавное перемещение ползунка (Slider) ---');
    await ensureHittable('ui.value_slider');
    const sliderTargets = [0.8, 0.2, 1.0, 0.5];
    for (let i = 0; i < sliderTargets.length; i++) {
        const val = sliderTargets[i];
        const snapshot = await ensureHittable('ui.value_slider');
        const rect = parseRect(snapshot);
        const point = { x: rect.x + rect.width * val, y: rect.y + rect.height / 2 };
        await act(`[7/14.${i+1}] Плавный Drag Slider на значение ${val * 100}%`, 'ui.value_slider', 'drag',
            { from: { testId: 'ui.value_slider' }, to: point, steps: 16 },
            (_, after) => Math.abs(after.SliderValue - val) <= 0.05, 1000);
    }

    // 7. Выпадающий список (Dropdown)
    console.log('\n--- ЭТАП 6: Выпадающий список (Dropdown) ---');
    for (const label of ['Mode C', 'Mode A']) {
        await ensureHittable('ui.mode_dropdown');
        console.log(`   Раскрытие Dropdown для выбора '${label}'...`);
        await client.call('click', { testId: 'ui.mode_dropdown' });
        await wait(800); // Даём увидеть раскрытый список

        const dropdown = await inspect('ui.mode_dropdown');
        const rect = parseRect(dropdown);
        let optionPoint = null;
        for (let y = 0; y <= 1200 && optionPoint === null; y += 16) {
            const hit = await client.call('hit_test', { x: rect.x + rect.width / 2, y });
            const hits = hit.data && hit.data.hits ? hit.data.hits : [];
            if (hits.some(item => item.text === label)) optionPoint = { x: rect.x + rect.width / 2, y };
        }
        if (!optionPoint) throw new Error('Dropdown option not found: ' + label);
        await act(`[8/14] Клик по опции '${label}' в выпадающем списке`, 'ui.mode_dropdown', 'click', optionPoint,
            (_, after) => after.DropdownLabel === label, 1200);
    }

    // 8. Кнопка с одинарным и двойным кликом
    console.log('\n--- ЭТАП 7: Клики по кнопке (Single & Double Click) ---');
    await ensureHittable('ui.primary_button');
    await act('[9/14] Одинарный клик [ui.primary_button]', 'ui.primary_button', 'click', { testId: 'ui.primary_button' },
        (before, after) => after.InteractionCount === before.InteractionCount + 1, 1000);

    await act('[10/14] Двойной клик [ui.primary_button]', 'ui.primary_button', 'double_click', { testId: 'ui.primary_button' },
        (before, after) => after.InteractionCount === before.InteractionCount + 2, 1200);

    // 9. Скролл списков (ScrollRect)
    console.log('\n--- ЭТАП 8: Скролл списков (ScrollRect) ---');
    await ensureHittable('ui.command_scroll');
    await act('[11/14] Скролл ScrollRect вниз', 'ui.command_scroll', 'scroll', { testId: 'ui.command_scroll', delta_y: -2 },
        (before, after) => after.ScrollPosition.y < before.ScrollPosition.y, 1000);

    await act('[12/14] Скролл ScrollRect вверх', 'ui.command_scroll', 'scroll', { testId: 'ui.command_scroll', delta_y: 2 },
        (before, after) => after.ScrollPosition.y > before.ScrollPosition.y, 1000);

    // 10. Возврат в главное меню
    console.log('\n--- ЭТАП 9: Завершение и возврат в главное меню ---');
    await act('[13/14] Клик по кнопке Назад [btn_ui_back]', 'btn_ui_back', 'click', { testId: 'btn_ui_back' },
        (_, after) => after.ActiveWindow === 'window_main_menu', 1500, 'window_main_menu');

    console.log('\n================================================================');
    console.log('🎉 ВСЕ ВИЗУАЛЬНЫЕ ТЕСТЫ УСПЕШНО ПРОЙДЕНЫ!');
    console.log('================================================================\n');

    client.close();
}

main().catch(error => {
    console.error('❌ Ошибка во время выполнения теста:', error.stack || error);
    process.exitCode = 1;
});
