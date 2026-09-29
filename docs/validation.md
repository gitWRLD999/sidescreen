# Release validation

Version 0.2 was built and tested on Windows x64 with an active physical display and an MTT virtual display. The non-UI suite passed 15 checks. The background-input suite passed 37 checks in Windows PowerShell 5.1, including Unicode and empty text, button invocation, system checkbox state, list selection, foreground/keyboard-focus preservation, observation reuse refusal, and unsupported/password/read-only/primary-screen refusal. Human pointer movement is permitted and was not treated as an input failure.

The previous 0.1.1 release also passed 17 live placement checks, monitor PNG capture and read-only ChatGPT computer-use capture of a disposable window on the agent screen. The current changes do not modify placement or capture code.

Coverage includes display identity, missing/ambiguous monitors, negative coordinates, full-window containment, JSON commands, native control state and refusal paths. The live target is a disposable WinForms application using standard controls. No broad third-party app compatibility claim follows from that test.

Limits: driver install/uninstall and monitor enable/disable were not exercised for this release. No reboot, new physical monitor, other GPU, ARM64 machine, HDR workload, remote protocol or third-party agent input backend is certified by these tests. Preview and screenshot rendering can vary by application and capture API; secure or hardware-protected surfaces may be blank.
