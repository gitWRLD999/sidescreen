---
name: sidescreen-work
description: Complete supported Windows desktop tasks on the SideScreen virtual monitor while the user keeps using their real desktops.
---

Use this plugin's MCP tools for the requested background task. Discover the
current display with `sidescreen_status`, then list its windows with
`sidescreen_windows`. Display IDs and handles can change; use the returned values.
`available:false` means the display boundary is unavailable. CUA `ready:false`
still permits supported classic named-control steps when `nativeObservation:true`.

This plugin supplies desktop computer use and is independent of Muse Link and
browser DOM automation. Use the current desktop tools, screenshots and returned
accessibility tokens. It does not launch or select a Chrome profile. Browser
account UI and page input are app-dependent; unsupported actions stop. Do not
inject the private keyboard/pointer DLL into a real account browser.

For a desktop task, lease one current window with `sideuser_open`, giving it a
short label. Observe that session, choose advertised actions on returned controls,
act once and verify resulting state. Close the session when finished. Use
`sideuser_run` for supported named-control sequences. If SideUser tools are not
available, `sidescreen_steps` and `sidescreen_act_and_observe` provide scoped
background control. Named steps prefer verified classic control messages and
freshen the target before each action. They never reuse tokens across steps.

Request a screenshot before coordinate input. Use its image dimensions and capture
binding, not monitor coordinates or invisible window borders. Supported SideUser
pointer, drag and keyboard actions use private input state; compatibility depends
on the app. Empty action lists do not authorize a foreground fallback.

Inspect `ok`, `stop`, focus and effect receipts. Verify the app's postcondition:
a CUA `effect:unverifiable` receipt is not proof of success. On a refusal, focus
change or unknown outcome, stop and observe; do not replay input or restore focus.
Call `sidescreen_stop` on a stop request; a fresh status starts a new connection
before resumed work. Human pointer movement alone is allowed. This system shares Windows focus, so
switching foreground apps can cause a conservative stop. A virtual monitor is
neither a separate Windows session nor a security sandbox.

Built-in Windows computer-use clicks activate their target and move the shared
pointer. They are not a fallback for a task that must preserve the user's focus.
Screenshots and returned content are sent to the calling assistant only
when a tool requests them. Limit observations to the authorized task window.
