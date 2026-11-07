// Explicit, local prototype checkpoints; never filesystem saves or Host evidence.
export const BUFFER_KEY = 'escode.preview.buffers.v1';
const LIMIT = 2_000_000;
const own = (value, key) => Object.prototype.hasOwnProperty.call(value, key);
export function validateCheckpoint(value, baseline) {
  if (!value || value.version !== 1 || value.workspace !== 'sample-v1' ||
      !Number.isSafeInteger(value.createdAt) || value.createdAt < 0 ||
      !value.buffers || typeof value.buffers !== 'object' || Array.isArray(value.buffers) ||
      !value.baseline || typeof value.baseline !== 'object' || Array.isArray(value.baseline) ||
      !Array.isArray(value.tabs) || !value.tabs.length) return null;
  const names = Object.keys(baseline);
  if (Object.keys(value.buffers).length !== names.length ||
      Object.keys(value.baseline).length !== names.length) return null;
  let size = 0;
  for (const name of names) {
    if (!own(value.buffers, name) || !own(value.baseline, name) ||
        value.baseline[name] !== baseline[name] || typeof value.buffers[name] !== 'string' ||
        value.buffers[name].length > 1_000_000) return null;
    size += value.buffers[name].length;
  }
  if (size > LIMIT || value.tabs.length > names.length ||
      new Set(value.tabs).size !== value.tabs.length ||
      value.tabs.some(name => !own(baseline, name)) || !value.tabs.includes(value.selected)) return null;
  return {version:1, workspace:'sample-v1', createdAt:value.createdAt,
    baseline:{...baseline}, buffers:{...value.buffers}, tabs:[...value.tabs], selected:value.selected};
}
export function readCheckpoint(baseline, storage) {
  try {
    const raw = storage?.getItem(BUFFER_KEY);
    if (!raw) return {status:'empty', checkpoint:null};
    if (raw.length > LIMIT * 6 + 100_000) return {status:'invalid', checkpoint:null};
    let value;
    try { value = JSON.parse(raw); }
    catch { return {status:'invalid', checkpoint:null}; }
    const checkpoint = validateCheckpoint(value, baseline);
    return {status:checkpoint ? 'ready' : 'invalid', checkpoint};
  } catch { return {status:'unavailable', checkpoint:null}; }
}
export function saveCheckpoint(session, baseline, storage) {
  try {
    const checkpoint = validateCheckpoint({...session, version:1, workspace:'sample-v1',
      baseline, createdAt:Date.now()}, baseline);
    if (!checkpoint || !storage) return false;
    storage.setItem(BUFFER_KEY, JSON.stringify(checkpoint));
    return true;
  } catch { return false; }
}
export function removeCheckpoint(storage) {
  try { if (!storage) return false; storage.removeItem(BUFFER_KEY); return true; }
  catch { return false; }
}
export function restoreCheckpoint(checkpoint, current, baseline) {
  const valid = validateCheckpoint(checkpoint, baseline);
  if (!valid) throw new Error('Invalid checkpoint');
  // All-or-nothing: restoring cannot silently replace current unsaved work.
  if (Object.keys(baseline).some(name => current[name] !== baseline[name] &&
      current[name] !== valid.buffers[name])) throw new Error('Conflicting unsaved buffer');
  return {buffers:{...valid.buffers}, tabs:[...valid.tabs], selected:valid.selected};
}
