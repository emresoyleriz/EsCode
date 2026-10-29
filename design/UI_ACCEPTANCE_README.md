# EsCode — Complete UI Acceptance Matrices

Status: QA checklist **NOT_TESTED**, not a claim that screen designs or component variants are implemented.

- SCREEN_ACCEPTANCE_MATRIX.csv includes 13 main screens × 14 checks = 182 evidence requirements.
- COMPONENT_STATE_MATRIX.csv includes 32 UI primitives/components × 12 states = 384 evidence requirements.

## Review rules
1. Accept a screen only with reviewed original premium dark/light mockups, interaction spec, keyboard/a11y and representative performance evidence.
2. Never accept screenshots blindly: compare content, visual hierarchy, ergonomics and user trust.
3. Audit layouts across monitor/DPI configurations, including 1366×768 and 1920×1080; preserve pinned panels and unsaved buffers.
4. Approval and OutcomeUnknown cannot be hidden behind success toast or animation.
5. User-controlled Git actions must remain separate.
6. Record concrete results, evidence URL, reviewer and timestamp in Issue comments before checking off entries. The CSV is a template, not proof.

UI-GATE #81 and UI-GAP #74–#80 remain OPEN until the evidence is reviewed.
