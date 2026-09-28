# Release validation

The release was built and tested on Windows x64 with an active physical display and an MTT virtual display. The non-UI suite passed 11 checks, and the live disposable-window placement suite passed 17 checks including agent-display placement, foreground and cursor preservation. `Status`, `Windows` and `Candidates` returned valid JSON. Capture wrote a PNG of the agent display. The Codex skill passed `quick_validate.py` and all PowerShell scripts parsed successfully.

Coverage: display identity and recovery selection, missing/ambiguous monitors, negative monitor coordinates, build correctness, JSON diagnostics, and a live move of a disposable nonactivating window with foreground/cursor checks.

Limits: driver install/uninstall and monitor enable/disable were not exercised for this release. No reboot, new physical monitor, other GPU, ARM64 machine, HDR workload, remote protocol or third-party agent input backend is certified by these tests. Preview and screenshot rendering can vary by application and capture API; secure or hardware-protected surfaces may be blank.
