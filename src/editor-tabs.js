// Stable, pure local editor-tab state. Closing a tab never discards its buffer.
export function openEditorTab(tabs, name) {
  if (typeof name !== "string" || !name || name.length > 255) return [...tabs];
  return tabs.includes(name) ? [...tabs] : [...tabs, name];
}
export function closeEditorTab(tabs, selected, name) {
  if (!tabs.includes(name) || tabs.length <= 1) return { tabs: [...tabs], selected };
  const index = tabs.indexOf(name);
  const next = tabs.filter(item => item !== name);
  return { tabs: next, selected: selected === name
    ? next[Math.min(index, next.length - 1)] : selected };
}
