# G9.1 — Palette keyboard and desktop viewport accessibility smoke (partial)

- Main sidebar now exposes a named navigation landmark and active page with aria-current.
- The command palette keeps keyboard focus inside the modal using Tab/Shift+Tab wrapping, supports ArrowUp/ArrowDown between its input and results, Enter selection, Escape dismissal and focus restoration.
- Search input has an accessible label and points at the available results container.
- Playwright smoke verifies palette navigation, keyboard activation of sidebar routes and 13-screen navigation at 1366x768, 1920x1080, 2560x1440 and 3440x1440.
- Existing screenshots remain TR/EN × dark/light × all 13 screens (52 images).

**This does not complete G9.1:** Actual 13 screens still contain disconnected previews; screen-reader and contrast audit, keyboard focus for all nested controls, mixed-DPI/real multiple monitors, drag docking, accessibility expert review and signed Windows GUI tests are still required.
