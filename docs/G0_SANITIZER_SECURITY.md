# G0 security remediation — Monaco DOMPurify

As of 2026-11-04, GitHub Actions failed the production npm audit because
monaco-editor 0.55.1 depends on vulnerable DOMPurify 3.2.7.

- \`package.json\` now overrides DOMPurify to 3.4.17.
- The lockfile pins the verified npm tarball integrity for 3.4.17.
- The \`postinstall\` hook replaces **only the ESM sanitizer module** in the local
  Monaco package with an import of DOMPurify 3.4.17. It fails closed if the
  expected module layout is absent or the installed version differs.
- CI runs \`npm ci\` and \`npm audit --omit=dev --audit-level=high\`, then bundles
  the ESM-based editor.

**Important:** Updating the npm dependency alone does not fix Monaco's embedded
copy. This workaround must be independently security-reviewed, and the final
bundle inspected. The AMD/min distributions, if used in future, are not covered.
Replace the workaround with a vetted upstream release when available.
No renderer privilege or HTML trust boundary was relaxed.
