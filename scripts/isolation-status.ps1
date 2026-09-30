[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
try {
    $computer=Get-CimInstance Win32_ComputerSystem
    $os=Get-CimInstance Win32_OperatingSystem
    $service=Get-CimInstance Win32_Service -Filter "Name='vmms'"
    $module=@(Get-Module -ListAvailable Hyper-V)
    $disks=@(Get-CimInstance Win32_LogicalDisk -Filter 'DriveType=3' | ForEach-Object {@{drive=$_.DeviceID;freeBytes=$_.FreeSpace}})
    @{ok=$true;os=$os.Caption;memoryBytes=$computer.TotalPhysicalMemory;hypervisorPresent=$computer.HypervisorPresent;hyperVManagementAvailable=($module.Count -gt 0 -and [bool]$service);vmServiceState=$service.State;disks=$disks;universalHostInputIsolation=$false;persistentGuestRequired=$true} | ConvertTo-Json -Depth 5
}catch{@{ok=$false;error=$_.Exception.Message} | ConvertTo-Json -Compress;exit 1}
