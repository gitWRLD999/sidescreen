# Release validation

Version 0.7 adds a separate activation-only Chrome guard. The installed Muse
Link 1.4 MCP proxy completed Chrome's actual FedCM account chooser against a
loopback identity provider with fictional accounts. Both the account button
and native Continue action preserved foreground, keyboard focus and cursor;
the fixture verified the completed identity exchange independently. Trusted
page pixel clicks, Unicode typing, Backspace and scrolling also passed with
preserved host input. The 26 non-UI and 12 Muse adapter checks passed. A real
Google/OpenTrain login, every Chrome prompt and universal isolation are not
certified by this fixture. Cold Chrome extension attachment can still activate
Chrome and is detected before subsequent business actions.

Version 0.6 adds the original x64/x86 user-mode virtual-input adapter. It passed
118 live checks against disposable 64-bit and 32-bit WinForms windows: standard
button effects and virtual cursor queries, Unicode insertion, Ctrl+A selection
and replacement, separate modifier state, virtual mouse capture and drag,
vertical/horizontal wheel input, double-clicks, activation/cursor suppression,
one-use observations, password/read-only refusal and out-of-bounds gestures.
The 26 non-UI checks, 12 adapter checks, 37 native background checks and 61
CUA native/WPF checks also passed.

The installed Muse Link 1.3 proxy passed 31 two-client checks for window leases,
cross-client refusal through legacy SideScreen/assistance tools, actual app
handlers, physical cursor/focus preservation, private Unicode paste, named
keyboard macros, replay protection, release/reassignment and both labeled
cursors rendered by the production preview. In one local run, act-and-observe
had a 1.25-second median and two-step keyboard macros completed in 3.2–3.8
seconds, excluding remote network/model time. These are measured tool times,
not universal app benchmarks. Original native adapter code is MIT; bundled
MinHook retains BSD-2-Clause.

A read-only check also invoked the production human-cursor renderer while the
actual visible Windows pointer was on SideScreen. It drew 96 non-background
pixels without moving the pointer. This checks rendering at the current layout;
other display scales and physical mouse movement were not independently tested.

The adapter is conditional user-mode API virtualization, not a separate Windows
input session or security sandbox. Raw/direct input, asynchronous app behavior,
custom input threads, protected/elevated apps and arbitrary third-party
compatibility were not certified. Regular Chrome keeps the existing DOM route;
no native adapter is injected into it. Cold extension attachment can still
change focus and is monitored separately. Startup configuration is retained;
a reboot, remote Muse host and built-in hosted ChatGPT backend were not tested.

Version 0.5 was built October 3, 2026 with CUA Driver 0.31.0. The non-UI suite
passed 26 checks, the Muse adapter passed 12, and the CUA suite passed 61 live
checks. The installed Muse Link proxy passed 42 additional end-to-end checks:
native/WPF control invocation, calibrated software-pointer movement and a raw
canvas click, independent blue preview markers, winapp inspection/search,
actual CPU YOLOv9/EasyOCR inference, exact-window Office-compatible mutations,
and stale-token/wrong-display refusals. Real handler counters and document
readback verified effects. Foreground and keyboard focus stayed intact while
the human moved the mouse. Office COM providers on this PC are WPS Office;
actual Microsoft Office installations were not exercised.
The regular Chrome/native/WPF 32-check channel suite also passed again through
the installed 1.2 proxy after first attachment. Cold extension attachment changed
focus and was detected separately; subsequent tested actions preserved it.

SideCursor is an original scoped software-pointer backend with limited app
support. MouseMux vendor actuation was not enabled or certified. There is no
new Windows input session, universal isolation, VM, or hosted ChatGPT backend
integration. CPU visual inference took about 23 seconds including initial model
load/capture and remains an optional fallback. The human pointer's appearance
under actual physical movement on SideScreen is still not independently tested;
the blue agent marker is verified separately. Models and screenshots stayed
local; neither a remote Muse client nor a reboot was tested.

Version 0.4 was built October 3, 2026 on the same Windows x64 desktop with one
active MTT virtual display and separately installed CUA 0.31.0. The non-UI
suite passed 22 checks; native background input passed 37 live checks, CUA
passed 61 native/WPF live checks and the expanded Muse adapter passed 12 checks.
The persistent Muse Link agent MCP channel passed 32 end-to-end checks across
regular Chrome, a native app and a WPF app. Navigation, Unicode input, batched
buttons/checkboxes, screenshots, shared browser cookies, stale records and
incorrect display refusal were verified against real fixture state. Foreground
and keyboard focus remained intact during those actions. Human pointer movement
occurred during the successful test and was allowed. Timings and screenshots
remain local. No remote Muse network or reboot was tested.

Preview capture and cursor-composition code ran successfully on the actual
SideScreen bounds. The human cursor was not deliberately moved for this test;
its visibility under a physical pointer on that display remains unverified.
The viewer targets 10 frames per second. Fresh Playwright extension attachment
still activates Chrome's window, which the broker detects and reports. This
release does not promise universal input isolation or patch ChatGPT's backend.

Version 0.3 was built September 30, 2026 on Windows x64 with a physical desktop and the active MTT virtual display. The non-UI suite passed 22 checks. CUA integration passed 61 live checks through Windows PowerShell 5.1 against separately installed, Authenticode-valid Cua Driver 0.31.0. Native and WPF disposable apps received Unicode text, button invocations and checkbox changes while foreground and keyboard focus remained intact. Native edits used SideScreen messages; WPF input used CUA accessibility. CUA reported some actions as `effect:unverifiable`; independent fixture state proved their effects. Screenshots and token/capture bindings were also checked. Stale observations, foreground overrides, incorrect display IDs, out-of-image coordinates and native password/read-only edits were refused. The Muse engine adapter passed 12 checks. A local broker/MCP handshake is separate from proving a remote Muse connection.

No persistent Windows VM was provisioned or certified. An authorized search found no Windows installation image or Windows VM disk in accessible C:/E: locations. Read-only readiness checks found the Hyper-V management service/module unavailable. No reboot, guest login, arbitrary gestures or broad third-party app compatibility was tested.

Version 0.2 was built and tested on Windows x64 with an active physical display and an MTT virtual display. The non-UI suite passed 15 checks. The background-input suite passed 37 checks in Windows PowerShell 5.1, including Unicode and empty text, button invocation, system checkbox state, list selection, foreground/keyboard-focus preservation, observation reuse refusal, and unsupported/password/read-only/primary-screen refusal. Human pointer movement is permitted and was not treated as an input failure.

The previous 0.1.1 release also passed 17 live placement checks, monitor PNG capture and read-only ChatGPT computer-use capture of a disposable window on the agent screen. The current changes do not modify placement or capture code.

Coverage includes display identity, missing/ambiguous monitors, negative coordinates, full-window containment, JSON commands, native control state and refusal paths. The live target is a disposable WinForms application using standard controls. No broad third-party app compatibility claim follows from that test.

Limits: driver install/uninstall and monitor enable/disable were not exercised for this release. No reboot, new physical monitor, other GPU, ARM64 machine, HDR workload, remote protocol or third-party agent input backend is certified by these tests. Preview and screenshot rendering can vary by application and capture API; secure or hardware-protected surfaces may be blank.
