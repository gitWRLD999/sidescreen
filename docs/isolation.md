# An isolated agent desktop

For broad Windows mouse/keyboard compatibility without taking the user's host focus, run a persistent Windows VM and run computer-use inside the guest. A virtual monitor alone shares the host foreground window, keyboard focus and physical input queue. Different CUA cursors or sessions do not change this. Background CUA expands compatibility on the host but still has refusal cases.

## Persistent Windows route

1. Use a licensed Windows installation ISO or an existing Windows VM. Keep its disk persistent; closing a viewer must not discard it.
2. Install [Hyper-V on Windows Pro/Enterprise](https://learn.microsoft.com/en-us/windows-server/virtualization/hyper-v/get-started/Install-Hyper-V), or use an existing VM manager. Hyper-V installation needs an administrator and a restart. Stage this while the user can restart; do not restart a working desktop silently.
3. Create a generation-2 VM with its own persistent disk, 2 virtual CPUs and about 4 GB RAM as an initial configuration. Windows 11 additionally needs its documented TPM/Secure Boot and storage requirements. Adjust memory and disk capacity for guest apps and host resources. This guide does not provision or certify a VM.
4. Install the guest OS and apps, then install CUA inside the guest from its official distribution. Its daemon/broker must run in the guest's signed-in interactive desktop, not Session 0. Use an authenticated guest connection; retain host-key verification. Do not publish a raw computer-use port to the internet.
5. Let the agent call the guest driver directly. Foreground input inside that guest belongs to the guest, so a host viewer can remain hidden or on SideScreen. Sending host keyboard/mouse input into an RDP/VM viewer still requires host focus and defeats this architecture.
6. Verify simultaneous host typing/pointer movement and guest input, along with reconnect and restart behavior. VM input separation does not guarantee every app can be automated, nor does it remove app state, modal, anti-automation or privilege restrictions.

The guest has its own Chrome installation, profile and sign-in. Host Chrome cookies and profile locks cannot be treated as a portable guest login. Keep existing-account browser work on the established host browser automation route, or sign into the guest browser yourself. Do not silently copy a live Chrome profile or Windows credentials.

[Windows Sandbox](https://learn.microsoft.com/en-us/windows/security/application-security/application-isolation/windows-sandbox/) can be useful for a disposable trial. Closing it deletes installed software and state, so it is not the durable agent desktop requested here.

## Readiness checks

`scripts/isolation-status.ps1` reports OS, RAM, free disk, hypervisor presence and whether Hyper-V management is available. It is read-only and does not enumerate personal files. File discovery and VM selection are a separate, user-authorized step. An existing hypervisor can be used by WSL or security features; it does not prove that a Windows agent VM exists.

The development PC had Windows 11 Pro, about 16 GB RAM and about 65 GB free on C:. No Windows ISO or Windows VM disk was found in accessible C:/E: searches. WSL system disks were present. The Hyper-V VM management service/module was unavailable. An administrator feature setup plus a Windows installation image is still needed before this persistent VM route can be built and tested.
