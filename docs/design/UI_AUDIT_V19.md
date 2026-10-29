# EsCode — Premium UI Design Audit (V19)

Status: **Design gaps recorded, NOT accepted / NOT implemented**. This document supplements V18 Design System 1.1 without changing the 172 approved decisions.

## Product north star
Original Apple-inspired aesthetic and Windows-native behavior; premium visual quality, restrained transitions and legible code/diff/terminal surfaces. Default Midnight Glass and optional Pearl Light. Use semantic design tokens, accessible primitives, and custom IDE components rather than a generic template.

## Existing defined scope
Welcome Hub, Development Workspace, Agent Studio, Task Center, Code Intelligence, Git/Review, Approval Center, Model Studio, MCP Manager, Terminal/Debugger, Browser Automation, Settings/Theme/Keymaps, Observability/Recovery.

## Seven audited gaps (tracked as separate GitHub Issues)
1. Approved high-fidelity references for every core screen in dark and light modes (UI-GAP-01).
2. Per-screen action, navigation, keyboard, focus and error/undo interaction matrix (UI-GAP-02).
3. Agent task journeys with strong explainability, evidence, risk indicators and recovery (UI-GAP-03).
4. Docking/floating state, responsive breakpoint, mixed-DPI and monitor disconnect matrix (UI-GAP-04).
5. Complete component catalogue, variants and interaction state inventory (UI-GAP-05).
6. Measurable visual, accessibility and performance evidence (UI-GAP-06).
7. Brand usage and font/icon asset rights and Windows convention validation (UI-GAP-07).

## Non-negotiable UI acceptance gates
- The *story → plan → edit → test → review → user-controlled Git* journey must be possible with keyboard-only controls.
- Read-only, Ask and Supervised modes are clearly marked; Autonomous is not an MVP capability.
- Every destructive/privileged tool action shows exact scope, risk and a bound expiring approval; a toast is never a replacement for an approval.
- A dispatched command cannot display as successful until backend verification evidence is received.
- Each long-running task has queued/running/waiting approval/paused/failed/outcome unknown/completed states. Users may safely inspect and intervene.
- User-pinned dock state is never silently changed by automation.
- Uncommitted and unsaved buffers are never silently discarded.
- All screens provide applicable loading, empty, error, permission-denied, reconnect and offline states.
- Review design baselines at 1366×768, 1920×1080, 2560×1440 and ultrawide; DPI 100/125/150/200%; multiple monitors and monitor disconnect.
- Color and UI contrast target WCAG 2.2 AA; keyboard, focus order, announcements, reduced motion and text scaling test evidence required.
- Performance proposals: command palette open p95≤100ms; panel response p95≤150ms; typing input p95≤50ms under representative load; 60fps normal transitions. These are NOT yet measured.
- Visual regression changes require human-reviewed approval; do not auto-update baseline to hide regressions.

## Review protocol
For every screen attach: screen reference dark/light, UX state matrix, keyboard map, semantic token mapping, accessibility assessment, performance trace, snapshot test result, owner and reviewer approval. Keep the visual design approval separate from functionality/test evidence.

## Current assessment
The visual direction and component architecture are specified, but screen-by-screen high-fidelity artifacts and verified usability tests are still missing. **UI is NOT certified complete.** See UI-GAP Issues.
