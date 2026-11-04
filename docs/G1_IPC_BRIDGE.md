# G1 — Electron Main ↔ .NET Host authenticated IPC (review candidate)

Status: implementation candidate. Not production-security certified. Existing Windows sandbox, tool broker and durability release gates remain OPEN.

## Implemented scope

- Electron Main starts one .NET 10 Host process on Windows. Its session secret is passed through stdin, not on the process command line, preferences, or renderer IPC.
- The Host uses Windows Named Pipes with CurrentUserOnly and bounded 4096-byte frames. A randomly named pipe and random 256-bit HMAC-SHA256 secret are created per process.
- Only the capabilities method is supported. It uses a per-request random ID, version 1, HMAC request and separately authenticated response; the secret itself is never transmitted over the pipe.
- Unsupported commands, wrong HMAC, duplicate request IDs, malformed and oversized frames are denied. Request timeout is three seconds. The .NET Host watches its Electron parent process.
- Renderer gets only sanitized mode/protocolVersion/safeRenderer/hostConnected. Existing preload remains read-only: no new file, shell or model APIs.
- Missing/unreachable Host returns host-unavailable, never a false successful connected status.

## Windows validation

From repo root after applying the G0 foundation branch:

    npm ci
    npm test
    npm run build
    dotnet build .\src\host\EsCode.Host\EsCode.Host.csproj -c Release
    node --test tests/windows/ipc-host.test.mjs
    npm run desktop

A successful Electron capability query shows host-read-only. This does not mean agents, LLMs, file tools or terminals work.

## Negative and release gates

- The portable mock-server test rejects forged responses, mismatched request IDs, unexpected fields, oversized packets and timeout.
- The Windows integration test checks real Host handshake, denied wrong auth/replay/tool command/oversize and subsequent recovery.
- Pending: actual Windows CI evidence, independent reviewer, cross-user attack exercise, process impersonation tests, event protocol sequencing and replay, backpressure/reconnect, OS sandbox and secure tool broker.
- E02-S01 cannot close until Windows tests and reviewer evidence; E02-S02/S03 remain explicitly open.

## Threat boundary

Same-user attackers with debugger/process injection rights, local administrators, compromised Host binaries and hostile plugin execution are not solved by CurrentUserOnly or HMAC alone. Do not expose privileged methods until OS isolation, resource-scoped authorization and typed contracts are separately verified.
