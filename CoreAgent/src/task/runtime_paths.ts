import path from 'node:path';
/** Repository root (…/URDT), resolved from this file's location. */
export const ROOT = path.resolve(path.dirname(new URL(import.meta.url).pathname.replace(/^\/([A-Za-z]:)/, '$1')), '..', '..', '..');
