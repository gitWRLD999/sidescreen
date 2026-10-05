---
name: sidescreen
description: Use the SideScreen virtual Windows monitor for supported desktop work without taking the user's foreground focus. Route ChatGPT/Codex through the independent local desktop plugin or scoped CLI.
---

Use the independent SideScreen desktop plugin for ChatGPT/Codex work. Muse Link,
remote connectivity and regular Chrome DOM automation are separate integrations;
they are not dependencies of this desktop plugin.

Call `sidescreen_status`, then `sidescreen_windows`. Use current returned display
IDs and handles. Stop when the display is unavailable. CUA readiness is required
for CUA observations; supported named native steps can work with
`nativeObservation:true` even when CUA is unavailable.

Lease the selected window with `sideuser_open`; observe, act once and verify the
actual app result. Close the session when finished. Session cursors, keyboard
state and clipboard are private; Windows mutexes enforce window leases across
new plugin processes. Named steps inspect and uniquely match a control before
each action. Request a fresh screenshot before pixel gestures and use returned
image coordinates. No tokens or screenshot coordinates survive a new observation.

Check `ok`, `stop`, `dispatched`, effect and focus receipts. An unverifiable
transport effect is not proof of task completion. Stop on a refusal, focus change
or unknown outcome; do not replay an action or restore foreground focus. Human
pointer movement alone is allowed. Foreground app changes can conservatively
stop agent work. Raw input, elevated/security dialogs and some GPU/browser UI
are unsupported. Never fall back to global or built-in foreground input.

Use `sidescreen_stop` when asked to stop. MCP cancellation also closes its worker.
The plugin declares Stop/Interrupt hooks; host support and hook trust are needed.
A fresh status call is required before resumed work. Disabling the plugin does
not remove apps, profiles, files or the separate CUA service.

If MCP tools are unavailable, locate `agent.ps1` from `SIDESCREEN_HOME`, then
`%USERPROFILE%\AgentTools\SideScreen`, then `%LOCALAPPDATA%\SideScreenTools`.
Call `-Action Status` and `-Action Windows`. For classic input, read the installed
`docs/background-input.md`, use `Inspect`, then one `Act` with its returned
observation and advertised action. Each inspection is one-use and lasts two
minutes. For CUA, read `docs/cua.md`; observe a fully contained window and dispatch
one action using its returned token or screenshot. No foreground overrides.

Use `Candidates` only to locate a user-authorized window for placement. Re-read
Status and the handle before `Move -Destination Agent -ExpectedDisplayId <id>`.
Normal visible windows can be moved without activation; minimized/maximized
windows requiring activation must be restored by the user. Verify the placement
receipt. Never move unrelated user windows.

Built-in Windows computer-use input activates its target and is not a background
fallback. Its opaque window IDs are not Win32 handles. SideScreen provides an
independent local route; it does not patch the built-in backend. A virtual display
is not a separate input session or security sandbox. Limit observation and work
to the authorized task window. Muse-specific browser/account tooling belongs in
Muse Link's own package and operating guide.
