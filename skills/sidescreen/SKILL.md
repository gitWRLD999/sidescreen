---
name: sidescreen
description: Discover the SideScreen virtual Windows monitor and use scoped background input, screenshots, and focus-preserving placement. Use when the user wants agent work on their invisible screen without taking foreground focus.
---

# SideScreen

Muse Link 1.4 adds `chrome_desktop_observe` / `chrome_desktop_act` for the small
Chrome account chooser and other supported native browser controls. These
discover the exact owned Chrome window in the pinned regular profile and use
CUA with the separate activation-only guard; do not use SideUser's keyboard/
pointer DLL on an account browser. Webpage pixels use `chrome_visual_observe`
/ `chrome_visual_act`, while DOM `act` / `steps` remain the fast route for
webpage controls. Read Muse Link's `docs/chrome-desktop.md` and SideScreen's
`docs/chrome-focus.md`. Select only the authorized identity; password, MFA,
passkey and security barriers require human completion. Verify fresh images
and resulting state, and never replay an unknown action or restore focus.

Muse Link 1.3 exposes seven `sideuser_*` tools for per-agent window leases,
labeled cursors, private text clipboard and named macros. Read
`docs/sideusers.md` beside the installed tool before use. Discover current
display/windows, open a session, observe, act once and verify the returned
state. Only this MCP connection owns its session. Native x64/x86 virtual input
supports bounded drag, wheel, double-click and native Edit keyboard events;
use semantic `set_value`/`invoke` for other supported controls and regular Chrome
DOM for websites. Input remains serialized, and universal compatibility is not
established. Close sessions when finished. Unknown outcomes stop without replay.

Muse Link 1.2 optionally adds `winapp_inspect`/`winapp_find` for read-only UI search, exact-window UFO Word/Excel-compatible app APIs, local CPU `omniparser_observe`, and free `sidecursor_move`/`sidecursor_click`. Prefer regular Chrome DOM and semantic input for speed. Read `docs/sidecursor.md` before pointer use: it requires a fresh screenshot plus accessibility tree and corroborated capture transform, consumes one observation, and verifies focus. It cannot provide universal isolation. The preview's blue Agent marker is independent of the human cursor. `mousemux_status` only probes vendor connectivity; it does not enable unverified vendor actions.

Use the installed SideScreen `agent.ps1` script. Locate it from `SIDESCREEN_HOME` when set, then `%USERPROFILE%\AgentTools\SideScreen\agent.ps1`, then the legacy `%LOCALAPPDATA%\SideScreenTools\agent.ps1`. The shared user path avoids packaged AppData redirection differences.

Call `-Action Status` before acting. The `agentScreen` object supplies a current display ID, device name and bounds; `available=false` means stop and report the display condition. Windows display numbers can change after a topology change. Avoid guessing coordinates or treating a screenshot of the primary display as the agent screen.

`-Action Windows` lists only windows on SideScreen. Use `-Action Candidates` only to find a user-authorized window to move. `-Action Move` and `-Action Capture` require `-ExpectedDisplayId` from a fresh Status result. A changed ID is refused, and a placement receipt tells you whether foreground focus and cursor position stayed intact. Re-enumerate window handles before moving; old handles can refer to a different window later.

Before first input use, read `docs/background-input.md` beside the installed tool. Run `-Action Inspect -WindowHandle <handle> -ExpectedDisplayId <id>`. Choose an element by its name/type and advertised `Actions`. Run `-Action Act` with the same handle/display, `-ObservationId <observation.Id> -ElementId <element.Id> -Operation <action>`. `SetValue` also requires `-Value` and replaces all text. Actions are SetValue, Invoke, Toggle and Select for specific classic native controls. Each inspection permits one attempt and expires after two minutes; re-inspect after every action or refusal.

Read `ok`, `stop`, `dispatched`, `verification` and `focus`. On a timeout, unknown outcome or focus change, stop and observe; never repeat automatically or restore focus. Human pointer movement is allowed, so `CursorPreserved` is informational. Verify the resulting UI after button invocation. Empty actions mean unsupported, not permission to fall back to foreground input.

For modern controls, read `docs/cua.md` beside the installed tool and check `Status.cua.installed`. The optional CUA service must run in the same interactive Windows session. Use `-Action CuaObserve` with a fresh handle/display. Select a returned `state.elements[].element_token` or inspect the returned PNG before choosing screenshot pixels. Then use `-Action CuaAct -ObservationId <observationId> -Tool <tool> -ArgumentsJson '<JSON>'` with the same handle/display. Each observation allows one attempt and expires after two minutes. Classic native controls prefer the message backend; other CUA routes are experimental. Check `driver.effect` and verify a fresh app state even when `ok:true`. A refusal is a stopping point; raw CUA foreground/desktop tools bypass SideScreen's boundary and are not a background fallback.

Muse's authenticated broker can expose `sidescreen_status`, `sidescreen_windows`, `sidescreen_observe` and `sidescreen_act` through its `sidescreen` engine. The same current-display, observation and receipt rules apply. An SSH service should use that interactive broker, rather than launching desktop input directly in Session 0.

Muse Link 1.1's combined `agent` channel adds `sidescreen_find`, `sidescreen_wait`, `sidescreen_act_and_observe` and `sidescreen_steps`. Observe defaults to tree-only; request a screenshot before pixel actions and use its returned dimensions. Named batches freshen and uniquely resolve a token before each step, stopping on failures or focus changes. For websites, prefer `open_url`, semantic DOM `act`/`steps` and compact `snapshot` in the explicitly pinned `regular_chrome` profile. `chrome_status` identifies that profile. Tab selection does not raise its window. The upstream extension may activate Chrome on a fresh connection; check the focus receipt, stop and inspect rather than replaying input. Persistent MCP or JSON-lines CLI over one SSH connection avoids reconnecting for each command.

Observe with `Capture` or a read-only computer-use snapshot. Match exactly one returned app/title against the scoped Windows list; its opaque computer-use ID is not necessarily a Win32 handle. Built-in ChatGPT Windows clicks/typing activate their target; route supported actions through SideScreen instead. For browser content prefer a separately verified DOM route. If no background route supports an action, explain the limitation. App handlers can activate dialogs themselves; monitoring detects changes during its bounded observation period, not all future behavior. Never interact with unrelated windows. This is not a separate input session or a security sandbox.

See `docs/agents.md` beside the installed tool for the operating contract. Commands require the signed-in interactive Windows desktop. A virtual display and separate synthetic cursors still share host focus and input. For broad independent input, use a driver inside a separate persistent VM, as described in `docs/isolation.md`; clicking a VM viewer with host input defeats that separation. An isolated guest needs its own browser profile/sign-in. Built-in ChatGPT computer use is not patched by this installation.
