import { test } from 'node:test';
import { strict as assert } from 'node:assert';
import { readFileSync, readdirSync } from 'node:fs';
import { join } from 'node:path';

const read = path => readFileSync(path, 'utf8');
const json = path => JSON.parse(read(path));

test('NPM manifest and lock are reproducible and exact', () => {
  const pkg = json('package.json');
  const lock = json('package-lock.json');
  assert.equal(pkg.private, true);
  assert.equal(lock.lockfileVersion, 3);
  assert.equal(lock.name, pkg.name);
  assert.equal(lock.version, pkg.version);
  const root = lock.packages[''];
  assert.ok(root, 'missing root lock entry');
  for (const section of ['dependencies', 'devDependencies']) {
    assert.deepEqual(root[section] ?? {}, pkg[section] ?? {}, section + ' lock drift');
    for (const [name, version] of Object.entries(pkg[section] ?? {})) {
      assert.match(version, /^\d+\.\d+\.\d+(?:-[\w.-]+)?$/, name + ' must be an exact version');
    }
  }
});

test('Supported Node and .NET SDK bands are explicit', () => {
  const node = read('.node-version').trim();
  assert.match(node, /^22\.\d+\.\d+$/);
  const sdk = json('global.json').sdk;
  assert.match(sdk.version, /^10\.0\.\d+$/);
  assert.equal(sdk.rollForward, 'latestFeature');
  assert.equal(sdk.allowPrerelease, false);
  assert.match(json('package.json').engines.node, /^>=22\./);
  assert.match(read('.npmrc'), /^engine-strict=true$/m);
  assert.match(read('.npmrc'), /^save-exact=true$/m);
});

test('Renderer imports do not cross into native or Electron process APIs', () => {
  const files = [];
  function scan(dir) {
    for (const entry of readdirSync(dir, { withFileTypes: true })) {
      const file = join(dir, entry.name);
      if (entry.isDirectory() && entry.name !== 'host') scan(file);
      else if (entry.isFile() && /\.(?:js|jsx|ts|tsx)$/.test(file)) files.push(file);
    }
  }
  scan('src');
  assert.ok(files.length > 0);
  const nativeImport = /(?:\bfrom\s*|\brequire\s*\(\s*|\bimport\s*\(\s*)['"](?:node:)?(?:electron|fs|child_process|net|tls|os|worker_threads)(?:\/[^'"]*)?['"]/;
  for (const file of files) {
    const code = read(file);
    assert.doesNotMatch(code, nativeImport, file + ' imports native APIs');
    assert.doesNotMatch(code, /\bipcRenderer\b/, file + ' bypasses the preload boundary');
  }
});

test('CI checks every PR and builds on Windows', () => {
  const workflow = read('.github/workflows/ui-foundation.yml');
  assert.match(workflow, /^  pull_request:\s*$/m);
  assert.match(workflow, /^  workflow_dispatch:\s*$/m);
  assert.doesNotMatch(workflow, /^\s+paths:\s*$/m, 'path filters could bypass required checks');
  assert.match(workflow, /node-version-file:\s*["']?\.node-version/);
  assert.match(workflow, /npm ci --no-audit --no-fund/);
  assert.match(workflow, /npm audit --omit=dev --audit-level=high/);
  assert.match(workflow, /^  windows-ui-build:\s*$/m);
  assert.match(workflow, /^  windows-ipc-spike:\s*$/m);
});

test('Secrets and generated native outputs are ignored', () => {
  const ignore = read('.gitignore');
  for (const fragment of ['.env', 'node_modules/', 'dist/', '**/bin/', '**/obj/', '.vs/']) {
    assert.ok(ignore.includes(fragment), fragment + ' missing from .gitignore');
  }
});
