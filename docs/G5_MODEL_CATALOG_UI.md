# G5 — Authenticated local model discovery in Model Studio (partial)

Model Studio can now request a **read-only model listing check** from the real .NET Host through a typed, signed Electron Main ↔ Host IPC command. Both sides authenticate the request and the response using the session HMAC and per-request nonce. The renderer receives only a boolean model-found result and a sanitized status code, never the model list or raw server body.

Only literal loopback IP addresses and HTTP `/v1/models` are accepted; credentials, URL query parameters, redirects, model tool calls and arbitrary network targets are denied. The Host caps the JSON response to 256 KiB and applies an end-to-end 5-second deadline. No API key is transmitted or persisted. The UI explicitly says that model discovery does not certify chat, SSE streaming, tools, or JSON mode.

CI exercises a mock provider handler, overlong body, 401, missing model, invalid requests, cryptographic signature rejection, plus real Windows Electron Main ↔ .NET Host ↔ HTTP loopback model discovery.

**G5 still open:** secure API-key vault, remote/cloud/company providers, human-approved provider profiles, real in-IDE chat streaming, tool calling, usage accounting and cancellation. The model probe does not grant agent tools or filesystem access.
