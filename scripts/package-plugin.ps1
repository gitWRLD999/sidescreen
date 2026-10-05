[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$OutputDirectory)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot
$plugin=Join-Path $root 'plugins\sidescreen'
$output=[IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $output -Force | Out-Null
$stage=Join-Path $output ('plugin-stage-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $stage | Out-Null
$files=@('plugin.json','mcp.json','README.md','assets\logo.png','skills\sidescreen-work\SKILL.md','scripts\start-mcp.ps1','runtime\server.cjs','runtime\THIRD-PARTY-LICENSES.txt','native\SideScreen.Cua.exe','native\SideScreen.Input.exe','native\SideScreen.Core.dll','native\SideScreen.Virtual32Host.exe','native\SideScreen.VirtualInput.dll','native\SideScreen.VirtualInput32.dll','native\start-cua.ps1','native\LICENSE','native\MinHook-LICENSE.txt')
foreach($name in $files){
    $source=Join-Path $plugin $name
    if(!(Test-Path -LiteralPath $source)){throw "Missing plugin asset: $name. Build native tools and mcp first."}
    $destination=Join-Path $stage $name
    New-Item -ItemType Directory -Path (Split-Path $destination) -Force | Out-Null
    Copy-Item -LiteralPath $source -Destination $destination
}
$zip=Join-Path $output 'SideScreen-Desktop-Plugin-1.0.0.zip'
Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip -Force
# A second archive is directly installable as a local marketplace.
$market=Join-Path $output ('marketplace-stage-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path (Join-Path $market '.agents\plugins'),(Join-Path $market 'plugins') -Force | Out-Null
Copy-Item -LiteralPath $stage -Destination (Join-Path $market 'plugins\sidescreen') -Recurse
Copy-Item -LiteralPath (Join-Path $root '.agents\plugins\marketplace.json') -Destination (Join-Path $market '.agents\plugins\marketplace.json')
Copy-Item -LiteralPath (Join-Path $root 'docs\evaluation-20261005.md'),(Join-Path $root 'docs\plugin-submission.md'),(Join-Path $root 'docs\privacy.md') -Destination $market
$marketZip=Join-Path $output 'SideScreen-Desktop-Marketplace-1.0.0.zip'
Add-Type -AssemblyName System.IO.Compression.FileSystem
if(Test-Path -LiteralPath $marketZip){Remove-Item -LiteralPath $marketZip}
[IO.Compression.ZipFile]::CreateFromDirectory($market,$marketZip)
$hashes=@($zip,$marketZip) | Get-FileHash -Algorithm SHA256 | ForEach-Object { $_.Hash.ToLowerInvariant()+'  '+[IO.Path]::GetFileName($_.Path) }
[IO.File]::WriteAllLines((Join-Path $output 'SHA256SUMS.txt'),$hashes,(New-Object System.Text.UTF8Encoding($false)))
Get-Item -LiteralPath $zip,$marketZip | Select-Object FullName,Length
# Staging folders are left intact for review; no recursive deletion is needed.
