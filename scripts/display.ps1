[CmdletBinding()]
param([ValidateSet('Status','On','Off')][string]$Mode='Status')
$ErrorActionPreference='Stop'
Add-Type -Path (Join-Path $PSScriptRoot 'SideScreen.Core.dll')
function Find-MttDevice {
    $matches=@(Get-PnpDevice -Class Display | Where-Object {
        $ids=(Get-PnpDeviceProperty -InstanceId $_.InstanceId -KeyName DEVPKEY_Device_HardwareIds -ErrorAction SilentlyContinue).Data
        $ids -contains 'Root\MttVDD'
    })
    if($matches.Count -ne 1){throw 'Exactly one installed MTT Virtual Display Driver is required. See the README for installation.'}
    $matches[0]
}
$device=Find-MttDevice
$paths=@([DisplayAudit]::Read())
if($Mode -eq 'Status') {
    @{device=$device.InstanceId;problemCode=[int]$device.Problem;displays=$paths} | ConvertTo-Json -Depth 5
    return
}
if($Mode -eq 'Off' -and !@($paths | Where-Object IsUsableMainDisplay).Count){throw 'A physical display must be active before disabling the virtual display. Open Windows Display Settings and enable it first.'}
if(@($paths | Where-Object IsVirtualMonitor).Count -gt 1){throw 'Multiple MTT monitors are active. Configure one virtual monitor before using this version.'}
$isAdmin=([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if(!$isAdmin) {
    $child=Start-Process -FilePath "$env:SystemRoot\System32\WindowsPowerShell\v1.0\powershell.exe" -Verb RunAs -WindowStyle Hidden -ArgumentList @('-NoProfile','-File',('"{0}"' -f $PSCommandPath),'-Mode',$Mode) -PassThru
    if(!$child.WaitForExit(90000)){throw 'Display change is awaiting elevation or device completion. Do not start another change yet.'}
    if($child.ExitCode -ne 0){throw 'Display change failed or was canceled. Run display.ps1 -Mode Status for current state.'}
    return
}
$guard=New-Object Threading.Mutex($false,'Local\SideScreen.DisplayChange')
$held=$false
try {
    $held=$guard.WaitOne(0)
    if(!$held){throw 'Another display operation is running.'}
    # Recheck at action time; never disable the sole usable display.
    if($Mode -eq 'Off') {
        if(!@([DisplayAudit]::Read() | Where-Object IsUsableMainDisplay).Count){throw 'The physical display disappeared; virtual display left on.'}
        Disable-PnpDevice -InstanceId $device.InstanceId -Confirm:$false
    } else { Enable-PnpDevice -InstanceId $device.InstanceId -Confirm:$false }
    $expected=if($Mode -eq 'On'){0}else{22}
    $confirmed=$false
    for($attempt=0;$attempt -lt 20;$attempt++) {
        $current=Get-PnpDevice -InstanceId $device.InstanceId
        $active=@([DisplayAudit]::Read() | Where-Object IsVirtualMonitor).Count
        if([int]$current.Problem -eq $expected -and (($Mode -eq 'On' -and $active -eq 1) -or ($Mode -eq 'Off' -and $active -eq 0))){$confirmed=$true;break}
        Start-Sleep -Milliseconds 500
    }
    if(!$confirmed){throw 'Windows has not confirmed the display change. Check Display Settings and device status.'}
} finally {if($held){$guard.ReleaseMutex()};$guard.Dispose()}
