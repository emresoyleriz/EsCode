# EsCode Versioned Contracts

These 15 schemas are a **first machine-readable representation** of the V14 logical contracts, not a replacement for security reviews. JSON Schema 2020-12. Required ContractVersion=1; unknown properties are rejected.

## Ownership
.NET Host is the source of truth for task, operation, approval and workspace state. Renderer does not directly call tools. A signed grant is scoped to actor/tool/resource/args digest/expiry. Unknown outcomes are reconciled before retries. Provider capability override never bypasses security policies.

## Validation still required
- Format validation (date-time, identifier constraints) in the selected validator implementation.
- Codegen integration and compatibility tests in .NET/TypeScript.
- Cryptographic authentication, per-session IPC, runtime ACL checks and adversarial tests.
- JSON Schema is **not** an authorization boundary.

Contract schema changes require versioned migration / ADR. Input payloads and opaque nested objects need stricter typed schemas before production.
