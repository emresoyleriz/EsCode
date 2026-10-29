# EsCode — UI Agent Development Rules

Read docs/design/UI_AUDIT_V19.md and docs/design/SCREEN_AND_INTERACTION_SPEC.md before altering UI; these extend the approved V14 architecture decisions.

1. Never replace the premium original UI with an unstyled template or generic admin dashboard.
2. React/Electron/Monaco boundaries are fixed; renderer cannot invoke OS tools directly.
3. Prefer semantic design tokens over hardcoded colors and custom accessible IDE components over ad-hoc markup.
4. Preserve edit buffers, pinned panes, docking/floating layout, versioned snapshots and safe window restore.
5. Every new screen/component requires dark/light and relevant high-contrast visual states, keyboard/focus behavior, empty/loading/error/offline states, and TR/EN overflow handling.
6. A tool/agent state is only successful when backend evidence verifies it. 'OutcomeUnknown' is a named user-visible state, not a generic success/failure toast.
7. Approval surfaces must clearly state actor, tool, target resources, arguments summary, risk, expiry, approve/deny scope; never conceal approval in animations.
8. Respect reduced motion, screen readers, 100–200% DPI, multi-monitor and performance budgets. Use virtualization for large logs/trees.
9. Add or update component catalogue, screenshots, interaction tests and visual regression references. No blanket screenshot update.
10. Keep user-controlled Git write authority; MVP supports Read-only, Ask and Supervised only. Do not enable Autonomous without P0 gates.
11. Report design gaps and obtain UI ADR for breaking design changes.
12. Do not mark implementation/test/security acceptance complete based only on written documentation.
