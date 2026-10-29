# AI Development Rules — EsCode

- Do not weaken authenticated IPC, tool broker, OS isolation or approval grant policies.
- Do not implement autonomous execution in MVP.
- Do not silently overwrite unsaved buffers, worktree changes or protected branches.
- Do not retry OutcomeUnknown without reconciliation.
- Do not claim source code is tested unless reproducible test log and result exist.
- Maintain premium original UI and full keyboard/accessibility/state quality; see .AI/UI_AGENT_RULES.md and #81.
- Use semantic tokens rather than hardcoded colors in new UI components.
- No secret values in logs, prompts, exports, Issue bodies or PRs.
- Service boundaries and contract schemas require review before incompatible changes.
- Do not modify main directly unless explicitly authorized; prefer review PRs.
