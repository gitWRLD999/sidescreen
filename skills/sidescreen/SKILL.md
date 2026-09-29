---
name: sidescreen
description: Discover the SideScreen virtual Windows monitor and use scoped background input, screenshots, and focus-preserving placement. Use when the user wants agent work on their invisible screen without taking foreground focus.
---

# SideScreen

Use the installed SideScreen `agent.ps1` script. Locate it from `SIDESCREEN_HOME` when set, then `%LOCALAPPDATA%\SideScreenTools\agent.ps1`, or ask for the extracted SideScreen folder if neither is available.

Call `-Action Status` before acting. The `agentScreen` object supplies a current display ID, device name and bounds; `available=false` means stop and report the display condition. Windows display numbers can change after a topology change. Avoid guessing coordinates or treating a screenshot of the primary display as the agent screen.

`-Action Windows` lists only windows on SideScreen. Use `-Action Candidates` only to find a user-authorized window to move. `-Action Move` and `-Action Capture` require `-ExpectedDisplayId` from a fresh Status result. A changed ID is refused, and a placement receipt tells you whether foreground focus and cursor position stayed intact. Re-enumerate window handles before moving; old handles can refer to a different window later.

Before first input use, read `docs/background-input.md` beside the installed tool. Run `-Action Inspect -WindowHandle <handle> -ExpectedDisplayId <id>`. Choose an element by its name/type and advertised `Actions`. Run `-Action Act` with the same handle/display, `-ObservationId <observation.Id> -ElementId <element.Id> -Operation <action>`. `SetValue` also requires `-Value` and replaces all text. Actions are SetValue, Invoke, Toggle and Select for specific classic native controls. Each inspection permits one attempt and expires after two minutes; re-inspect after every action or refusal.

Read `ok`, `stop`, `dispatched`, `verification` and `focus`. On a timeout, unknown outcome or focus change, stop and observe; never repeat automatically or restore focus. Human pointer movement is allowed, so `CursorPreserved` is informational. Verify the resulting UI after button invocation. Empty actions mean unsupported, not permission to fall back to foreground input.

Observe with `Capture` or a read-only computer-use snapshot. Match exactly one returned app/title against the scoped Windows list; its opaque computer-use ID is not necessarily a Win32 handle. Built-in ChatGPT Windows clicks/typing activate their target; route supported actions through SideScreen instead. For browser content prefer a separately verified DOM route. If no background route supports an action, explain the limitation. App handlers can activate dialogs themselves; monitoring detects changes during its bounded observation period, not all future behavior. Never interact with unrelated windows. This is not a separate input session or a security sandbox.

See `docs/agents.md` beside the installed tool for the operating contract. Commands require the signed-in interactive Windows desktop. Remote agents need an existing authenticated broker in that session; an SSH service session may not see its windows. No SideScreen server or Muse-broker adapter is included.
