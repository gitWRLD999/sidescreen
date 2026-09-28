[CmdletBinding()]
param([switch]$Test)
$ErrorActionPreference='Stop'
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if(!(Test-Path -LiteralPath $compiler)){throw 'The Windows x64 .NET Framework C# compiler is required.'}
$output=Join-Path $PSScriptRoot 'dist'
New-Item -ItemType Directory -Path $output -Force | Out-Null
$core=Join-Path $output 'SideScreen.Core.dll'
& $compiler /nologo /target:library /platform:x64 "/out:$core" /r:System.Drawing.dll (Join-Path $PSScriptRoot 'src\DisplayAudit.cs') (Join-Path $PSScriptRoot 'src\WindowTools.cs')
if($LASTEXITCODE -ne 0){throw 'Core build failed'}
& $compiler /nologo /target:winexe /platform:x64 "/out:$output\SideScreen.exe" "/r:$core" /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Core.dll (Join-Path $PSScriptRoot 'src\AppPaths.cs') (Join-Path $PSScriptRoot 'src\Tray.cs') (Join-Path $PSScriptRoot 'src\Preview.cs')
if($LASTEXITCODE -ne 0){throw 'App build failed'}
Copy-Item -Path (Join-Path $PSScriptRoot 'scripts\*.ps1') -Destination $output -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'README.md'),(Join-Path $PSScriptRoot 'LICENSE') -Destination $output -Force
$skillOutput=Join-Path $output 'skills\sidescreen'
New-Item -ItemType Directory -Path $skillOutput -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'skills\sidescreen\SKILL.md') -Destination $skillOutput -Force
$docsOutput=Join-Path $output 'docs'
New-Item -ItemType Directory -Path $docsOutput -Force | Out-Null
Copy-Item -Path (Join-Path $PSScriptRoot 'docs\*.md') -Destination $docsOutput -Force
if($Test){
    & $compiler /nologo /target:exe /platform:x64 "/out:$output\SideScreen.Tests.exe" "/r:$core" /r:System.Drawing.dll /r:System.Windows.Forms.dll (Join-Path $PSScriptRoot 'tests\Tests.cs')
    if($LASTEXITCODE -ne 0){throw 'Test build failed'}
    & "$output\SideScreen.Tests.exe"
    if($LASTEXITCODE -ne 0){throw 'Tests failed'}
}
Write-Host "Built $output\SideScreen.exe"
