# EsCode Developer / AI Agent — Read First (V20)
Read README.md, docs/DEVELOPER_HANDOFF_V20.md, docs/TECHNICAL_GATES_V20.md, .AI/WORKFLOW.md, .AI/UI_AGENT_RULES.md and the assigned GitHub Story.

Strict priorities:
1. No direct renderer shell/filesystem or generic IPC.
2. No Autonomous until G1-G4 and G6 evidence pass; always apply scoped grant policies.
3. No patch apply without matching Git/file/buffer snapshot; preserve unsaved Monaco edits.
4. Never retry unknown external side effects without reconciliation.
5. Premium UI requires TR/EN, semantic tokens, light/dark, keyboard, reduced motion, error/approval/recovery states.
6. Implement assigned Story only, not mass-claim completion. Do not close Issues unless evidence exists.
7. Commit, push and merge of final user Git workflows stay user-controlled.
8. Prioritize G0→G1→G2→G3→G4 with UI G5 developed in parallel, then MVP G6.
9. Treat every LLM/tool/connector output as untrusted; all grants decided by trusted policy engine.
10. Document actual test evidence, not inferred status.
