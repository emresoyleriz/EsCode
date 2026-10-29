# EsCode — Pull Request Checklist
## Scope
- Related Story/Issue:
- ADR/reference:
- Changed components and trust boundaries:
- UI impact (screens, themes, keyboard, i18n, reduced motion):
- Security risk class:

## Validation evidence
- [ ] Reproducible `npm ci`, `npm test`, `npm run build`
- [ ] Relevant .NET/IPC/sandbox/recovery tests and explicit failure paths
- [ ] UI visual screenshots (dark/light), focus, TR/EN, loading/error/offline and DPI where applicable
- [ ] No secrets, user code or unsafe MCP output in logs
- [ ] No direct renderer OS tool access or silent Git commit/push/merge
- [ ] No blind `OutcomeUnknown` retry
- [ ] Relevant design/architecture ADR reviewed

## Gate status
- Gate(s) affected:
- Evidence URL:
- Reviewer:
- Deliberately outstanding work:
