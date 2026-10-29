# EsCode Architecture Baseline 1.1

**Status:** approved architecture choices; unverified technical capabilities. Supersedes no V14 design decision.

## Components
- Electron Main manages windows, process lifecycle, preload API and authenticated IPC client.
- React renderer with Monaco owns visual state, not OS execution.
- .NET Application Host owns workspace state, task DAG, agent orchestration, policy, recovery and SQLite.
- Authenticated Named Pipes with per-user ACL, version negotiation, identity validation, bounded event replay and idempotent commands.
- Tool Broker is the only path to shell/OS, external MCP or browser actions and enforces risk-scoped capability grants.
- Global and per-workspace SQLite state kept outside repositories; artifacts separately hashed. WAL and one-writer-per-db.
- Local and cloud OpenAI-compatible providers have capability probes; never treat model-generated claims as authoritative.

## MVP
Windows 11 x64. Read-only, Ask and Supervised agent permissions; no Autonomous until all P0 security gates. Story→plan→edit→test→review→user-controlled Git. Never automatically commit, push or merge.

## Safeguards
- Unsaved buffer snapshots and worktree/git revision matching before applying a patch.
- Unknown external side effects require reconciliation before retry.
- Privileged commands show actor, exact scope, args digest, risk, expiry and decision before execution.
- Plugin trust manifest and OS isolation fail closed if secure capability is absent.
- Main protected branch planned; actual GitHub protection configuration must be independently verified.

## Prototype limitations
src/ is a browser-rendered UI proof of concept; Electron, .NET, OS sandbox, IPC, persistence, code intelligence, live models and real multi-agent execution are not implemented.

## Authority
V14 decisions → reviewed ADR/architecture documentation → machine-readable contract → code/tests → acceptance evidence. Security and design acceptance remain separate release gates.
