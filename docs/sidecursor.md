# Free scoped software pointer

SideScreen 0.5 includes the original MIT SideCursor backend, exposed by Muse
Link 1.2 as `sidecursor_move` and `sidecursor_click`. It gives supported Windows
apps an agent pointer independent of the human cursor, without virtual HID
installation, unsigned drivers or a MouseMux license.

Use a fresh `sidescreen_observe` with screenshot and accessibility tree. Pass
its observation ID, exact HWND/display ID and image-pixel position. CUA frames
are corroborated against native UIA before constructing the capture transform.
Invisible DWM borders and capture dimensions must not be guessed from the
outer window rectangle. Missing calibration, stale tokens, process changes,
moved/minimized/disabled windows and partial display containment are refused.
Each observation permits one attempt and expires in 120 seconds.

A left click on one uniquely observed supported button, checkbox or list item
uses the existing native/CUA background control route. Other client-area
pointer events use bounded Win32 window messages, targeted only at children of
the same scoped window/process. Titlebars, unrelated windows, global mouse
injection and keyboard injection are excluded. Control handlers can ignore
messages or activate dialogs; check focus receipts and fresh application state.
Delivery is not proof of a click effect, and unknown outcomes are not retried.

The preview draws a blue crosshair labeled Agent from the scoped pointer
marker, separately from the actual human pointer. Markers expire in two minutes
and disappear when their target no longer belongs to the display. Human cursor
movement remains informational in focus receipts; neither SideCursor nor
the preview sets the human cursor position.

This provides limited operation isolation for supported apps. It is not a
MouseMux driver clone, a separate Windows input session or a security sandbox.
Use DOM for regular Chrome, app APIs when available, and scoped CUA/native
semantics for routine controls. Fully independent general input still needs
a separate VM/session or a separately verified multi-user input backend.
