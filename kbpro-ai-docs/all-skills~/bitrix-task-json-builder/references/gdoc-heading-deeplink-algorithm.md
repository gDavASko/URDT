# Алгоритм формирования deep-link на заголовок Google Docs

## Проблема

Google Docs использует **динамические heading ID** формата `h.xxxxxxxxxxxx`. Эти ID:
- Генерируются случайно при создании заголовка
- НЕ совпадают с `headingId` из внутренней KIX-модели
- НЕ могут быть угаданы или сгенерированы программно «по смыслу»
- Стабильны (не меняются между сессиями), но доступны только через экспорт

## Единственный надёжный метод: HTML-экспорт

### Шаг 1 — Получить HTML-экспорт вкладки

```
https://docs.google.com/document/d/{DOC_ID}/export?format=html&tab={TAB_ID}
```

Пример:
```
https://docs.google.com/document/d/1oLRH4aQV_ho23ajBKM3hjYxwZCEOjKkSmW3VKIrjuyk/export?format=html&tab=t.41qv7pqo9ksy
```

Этот URL возвращает **полный HTML документа** со всеми реальными атрибутами `id` у заголовков.

> **Ограничение**: Google caps HTML export at ~10 MB. Для больших документов используй Google Docs API `documents.get` как альтернативу.

### Шаг 2 — Извлечь heading ID из HTML

В экспортированном HTML каждый заголовок (`<h1>` – `<h6>`) содержит атрибут `id`:

```html
<h2 id="h.8l8zotj496id"><span>Движение робота</span></h2>
```

Парсить регулярным выражением:

```javascript
const regex = /id="(h\.[a-z0-9]+)"/g;
```

Для каждого совпадения — извлечь текст заголовка из ближайших `<span>` тегов и декодировать HTML-сущности (`&#1056;` → `Р`, `&rarr;` → `→`, и т.д.).

### Шаг 3 — Построить карту `заголовок → heading_id`

Результат парсинга — массив пар:

```
h.8l8zotj496id  =>  Движение робота
h.16mqdhmfqo1y  =>  Графика
h.au2qux13p66m  =>  Анимации
h.o6qjlcncitua  =>  3. Волк (Препятствие)
...
```

### Шаг 4 — Сформировать финальную ссылку

Формат рабочей deep-link на заголовок:

```
https://docs.google.com/document/d/{DOC_ID}/edit?tab={TAB_ID}#heading={HEADING_ID}
```

Где:
- `{DOC_ID}` — ID документа из URL (после `/d/`)
- `{TAB_ID}` — ID вкладки (после `?tab=`, формат `t.xxxxxxxxxxxx`)
- `{HEADING_ID}` — **ровно тот `id`**, который стоит в HTML-экспорте (формат `h.xxxxxxxxxxxx`)

Пример результата:
```
https://docs.google.com/document/d/1oLRH4aQV_ho23ajBKM3hjYxwZCEOjKkSmW3VKIrjuyk/edit?tab=t.41qv7pqo9ksy#heading=h.8l8zotj496id
```

## Референсный Node.js скрипт

```javascript
const fs = require('fs');

// html — содержимое HTML-экспорта (файл, загруженный по export URL)
const html = fs.readFileSync('exported_tab.html', 'utf-8');

function decodeHtmlEntities(str) {
  return str
    .replace(/&#(\d+);/g, (_, code) => String.fromCharCode(parseInt(code, 10)))
    .replace(/&rarr;/g, '→')
    .replace(/&ndash;/g, '–')
    .replace(/&mdash;/g, '—')
    .replace(/&amp;/g, '&')
    .replace(/&lt;/g, '<')
    .replace(/&gt;/g, '>');
}

const regex = /id="(h\.[a-z0-9]+)"/g;
let match;
const headingMap = new Map();

while ((match = regex.exec(html)) !== null) {
  const hId = match[1];
  const pos = match.index;
  const after = html.substring(pos, Math.min(html.length, pos + 800));
  const textMatches = after.match(/>([^<]{3,120})</g);
  const texts = textMatches
    ? textMatches
        .slice(0, 5)
        .map(t => decodeHtmlEntities(t.replace(/^>|<$/g, '').trim()))
        .filter(t => t.length > 2)
    : [];
  headingMap.set(hId, texts.join(' | '));
}

// Вывод карты
const DOC_ID = 'YOUR_DOC_ID';
const TAB_ID = 'YOUR_TAB_ID';

for (const [hId, text] of headingMap) {
  const url = `https://docs.google.com/document/d/${DOC_ID}/edit?tab=${TAB_ID}#heading=${hId}`;
  console.log(`${hId} => ${text}`);
  console.log(`  URL: ${url}\n`);
}
```

## Чего НЕЛЬЗЯ делать

| ❌ Неправильный метод | Почему не работает |
|---|---|
| Угадывать heading ID по смыслу | ID — случайный хеш, не связан с текстом |
| Брать `headingId` из KIX-модели | KIX headingId ≠ URL anchor |
| Парсить DOM через Playwright | Google Docs рендерит заголовки динамически, anchor'ы нестабильны |
| Использовать `#heading=h.` без проверки | Ведёт на начало документа, если ID неверный |

## Краткая памятка

1. `export?format=html&tab=TAB_ID` → получи HTML
2. `id="h.xxxxx"` → извлеки все heading ID
3. Сопоставь heading ID с текстом заголовка
4. Собери URL: `…/edit?tab=TAB_ID#heading=HEADING_ID`
5. Проверь каждую ссылку, прежде чем вставлять в задачу
