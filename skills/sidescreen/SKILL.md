---
name: sidescreen
description: Discover and use the SideScreen virtual Windows monitor for agent windows, screenshots, and focus-preserving placement. Use when the user wants work on their agent screen or asks to keep their primary screen and focus free.
---

# SideScreen

Use the installed SideScreen `agent.ps1` script. Locate it from `SIDESCREEN_HOME` when set, then `%LOCALAPPDATA%\SideScreenTools\agent.ps1`, or ask for the extracted SideScreen folder if neither is available.

Call `-Action Status` before acting. The `agentScreen` object supplies a current display ID, device name and bounds; `available=false` means stop and report the display condition. Windows display numbers can change after a topology change. Avoid guessing coordinates or treating a screenshot of the primary display as the agent screen.

`-Action Windows` lists only windows on SideScreen. Use `-Action Candidates` only to find a user-authorized window to move. `-Action Move` and `-Action Capture` require `-ExpectedDisplayId` from a fresh Status result. A changed ID is refused, and a placement receipt tells you whether foreground focus and cursor position stayed intact. Re-enumerate window handles before moving; old handles can refer to a different window later.

Observe the agent screen with `Capture` or a targeted window state. When using ChatGPT computer use, select exactly one returned app window whose app/title matches the scoped `Windows` list; its opaque window ID is not necessarily the Win32 handle. Choose an input backend that supports background operation for the target app. SideScreen places and captures windows but does not provide isolated mouse/keyboard input. ChatGPT computer-use Windows input automatically activates the target, even on this monitor. If maintaining focus is required and no background input route exists, explain the limitation and ask how the user wants to proceed. Never treat virtual monitor placement as permission to interact with unrelated windows.

See `docs/agents.md` beside the installed tool or in the repository for invocation examples and the operating contract. SideScreen has no server or secret; use the user's existing authenticated remote shell if controlling it from another machine.
