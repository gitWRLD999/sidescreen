# Chrome window activation guard

Muse Link 1.4 can inspect Chrome's actual window and use background CUA on
browser-native account choosers. Clicking a chooser's final Continue button
can cause Chrome itself to activate its window even when the driver used
background messages. The guard suppresses that application side effect for
the assigned window before the click. It does not restore focus after a steal.

The original MIT native source has a separate `SIDESCREEN_FOCUS_ONLY` build.
That DLL hooks only SetFocus, SetActiveWindow, SetForegroundWindow, ShowWindow
and SetWindowPos. It accepts only activation-lease and release frames; pointer
and keyboard dispatch frames are refused by that build. MinHook 1.3.4 remains
BSD-2-Clause. No kernel driver, subscription, MouseMux license or VM is used.

`SideScreen.ChromeFocus.exe` validates the current SideScreen, complete native
window bounds, Chrome process/start time and executable product metadata. It
requires 64-bit Chrome for now. The caller first proves that the active tab
and every tab in that window belong to its browser bridge. This is an internal
Muse Link operation, not a tool for guarding arbitrary human windows.

The protection lasts 120 seconds after the last native action, then passes
through. Unrelated Chrome windows pass through. If the person explicitly
foregrounds the guarded root, its normal focus operations pass through too.
The native DLL is pinned until Chrome exits; replacing it in a running browser
cannot upgrade its hooks. Close Chrome before a changed native guard
upgrade; regular and agent windows can share its browser process. The installer
keeps an identical DLL and refuses to replace a loaded changed DLL. This does
not suppress every possible Windows activation mechanism,
provide an OS security boundary, or certify all browser/security prompts.

Build through `native/prepare-native.ps1` then `build.ps1 -Test`. The runtime
needs ChromeFocus.exe, ChromeFocus.dll and the existing Core.dll/Input.exe
beside each other. `test/live-chrome-chooser.mjs` in Muse Link tests an actual
Chrome FedCM chooser with fictional accounts served on loopback. Inspect both
the focus receipt and resulting UI; a posted-event receipt alone is not proof
that an account was selected.
