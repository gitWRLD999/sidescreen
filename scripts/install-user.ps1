[CmdletBinding()]
param([switch]$StartTray,[switch]$StartAtLogin,[switch]$StartCua,[switch]$CuaAtLogin)
$ErrorActionPreference='Stop'
$source=$PSScriptRoot
$destination=Join-Path $env:USERPROFILE 'AgentTools\SideScreen'
$skillSource=Join-Path $source 'skills\sidescreen\SKILL.md'
$skillDestination=Join-Path $env:USERPROFILE '.codex\skills\sidescreen'
foreach($name in @('SideScreen.exe','SideScreen.Core.dll','SideScreen.Input.exe','SideScreen.Cua.exe','SideScreen.ChromeFocus.exe','SideScreen.ChromeFocus.dll','SideScreen.VirtualInput.dll','SideScreen.VirtualInput32.dll','SideScreen.Virtual32Host.exe','MinHook-LICENSE.txt','agent.ps1','background.ps1','cua.ps1','start-cua.ps1','display.ps1')) {
    if(!(Test-Path -LiteralPath (Join-Path $source $name))) { throw "Missing $name; extract the complete release first." }
}
if(!(Test-Path -LiteralPath $skillSource)) { throw 'Missing skills\sidescreen\SKILL.md; extract the complete release first.' }
$installedExe=Join-Path $destination 'SideScreen.exe'
if(@(Get-Process -Name SideScreen -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $installedExe }).Count) {
    throw 'Exit the running SideScreen tray app before installing this update.'
}
New-Item -ItemType Directory -Path $destination,$skillDestination -Force | Out-Null
function Same-InstalledFile([string]$name) {
    $installed=Join-Path $destination $name
    return (Test-Path -LiteralPath $installed) -and ((Get-FileHash -LiteralPath $installed).Hash -eq (Get-FileHash -LiteralPath (Join-Path $source $name)).Hash)
}
$guardPath=Join-Path $destination 'SideScreen.ChromeFocus.dll'
if((Test-Path -LiteralPath $guardPath) -and !(Same-InstalledFile 'SideScreen.ChromeFocus.dll')) {
    try {$guardFile=[IO.File]::Open($guardPath,[IO.FileMode]::Open,[IO.FileAccess]::Write,[IO.FileShare]::Read);$guardFile.Dispose()}
    catch {throw 'Close Chrome before updating its loaded activation guard, then rerun this installer.'}
}
foreach($name in @('SideScreen.exe','SideScreen.Core.dll','SideScreen.Input.exe','SideScreen.Cua.exe','SideScreen.ChromeFocus.exe','SideScreen.ChromeFocus.dll','SideScreen.VirtualInput.dll','SideScreen.VirtualInput32.dll','SideScreen.Virtual32Host.exe','MinHook-LICENSE.txt','agent.ps1','background.ps1','cua.ps1','start-cua.ps1','display.ps1','README.md','LICENSE')) {
    if(Same-InstalledFile $name){continue}
    Copy-Item -LiteralPath (Join-Path $source $name) -Destination (Join-Path $destination $name) -Force
}
Copy-Item -LiteralPath $skillSource -Destination (Join-Path $skillDestination 'SKILL.md') -Force
$docsSource=Join-Path $source 'docs'
if(Test-Path -LiteralPath $docsSource) {
    $docsDestination=Join-Path $destination 'docs'
    New-Item -ItemType Directory -Path $docsDestination -Force | Out-Null
    Copy-Item -Path (Join-Path $docsSource '*.md') -Destination $docsDestination -Force
}
Write-Host "Installed SideScreen commands at $destination"
Write-Host "Installed Codex skill at $skillDestination"
if(Test-Path -LiteralPath (Join-Path $source 'integrations')) {Copy-Item -LiteralPath (Join-Path $source 'integrations') -Destination $destination -Recurse -Force}
if($CuaAtLogin) {
    $startup=Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs\Startup'
    $shell=New-Object -ComObject WScript.Shell
    $shortcut=$shell.CreateShortcut((Join-Path $startup 'SideScreen-Cua.lnk'))
    $shortcut.TargetPath=Join-Path $env:WINDIR 'System32\WindowsPowerShell\v1.0\powershell.exe'
    $shortcut.Arguments='-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File "'+(Join-Path $destination 'start-cua.ps1')+'"'
    $shortcut.WorkingDirectory=$destination;$shortcut.Description='Optional SideScreen CUA background service';$shortcut.Save()
    Write-Host 'SideScreen CUA supervisor will start at user sign-in.'
}
if($StartAtLogin) {
    $startup=Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs\Startup'
    $shell=New-Object -ComObject WScript.Shell
    $shortcut=$shell.CreateShortcut((Join-Path $startup 'SideScreen.lnk'))
    $shortcut.TargetPath=Join-Path $destination 'SideScreen.exe'
    $shortcut.WorkingDirectory=$destination
    $shortcut.Description='SideScreen virtual display tray control'
    $shortcut.Save()
    Write-Host 'SideScreen will start at user sign-in.'
}
if($StartTray) { Start-Process -FilePath (Join-Path $destination 'SideScreen.exe') -WindowStyle Hidden }
if($StartCua) {
    Start-Process -FilePath (Join-Path $env:WINDIR 'System32\WindowsPowerShell\v1.0\powershell.exe') -ArgumentList ('-NoProfile -ExecutionPolicy Bypass -File "'+(Join-Path $destination 'start-cua.ps1')+'"') -WindowStyle Hidden
}
