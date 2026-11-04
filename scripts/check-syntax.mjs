import { readdirSync, statSync } from 'node:fs';
import { join } from 'node:path';
import { spawnSync } from 'node:child_process';

const roots = ['desktop', 'scripts', 'src', 'tests'];
const files = [];
function collect(dir) {
  for (const entry of readdirSync(dir, { withFileTypes: true })) {
    const path = join(dir, entry.name);
    if (entry.isDirectory()) {
      if (entry.name !== 'host' && entry.name !== 'node_modules') collect(path);
    } else if (entry.isFile() && /\.(?:cjs|mjs|js)$/.test(entry.name)) {
      files.push(path);
    }
  }
}
for (const dir of roots) collect(dir);
if (statSync('vite.config.js').isFile()) files.push('vite.config.js');
let failures = 0;
for (const file of files.sort()) {
  const run = spawnSync(process.execPath, ['--check', file], { encoding: 'utf8' });
  if (run.status !== 0) {
    failures++;
    console.error('[syntax] ' + file + '\n' + (run.stderr || run.error?.message || 'unknown error'));
  }
}
console.log('[syntax] Checked ' + files.length + ' JS/MJS/CJS files; JSX is validated by Vite build.');
if (failures) process.exitCode = 1;
