# G2.1 — Windows AppContainer adversarial fixture (partial)

Windows CI now verifies the zero-capability process can launch known fixed system binaries before treating negative probes as evidence. The probes attempt to read a private file outside the workspace, reach a live loopback listener and spawn a child process under a 1-process Job Object.

Only hardcoded system utilities are used. This is test-only code; it is not connected to Electron IPC or an agent. A failed positive control fails the entire test rather than being misreported as an access denial.

Run the Windows Host with the flag --appcontainer-adversarial-spike.

Acceptance still open: production AppContainer worker binary/transport; malicious executable, multi-user, symbolic-link, device namespace and inherited-handle probes; token privileges and network policy; negative Windows VM attack suite. Do not enable autonomous tools on this basis.
