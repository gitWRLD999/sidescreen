[CmdletBinding()]
param([string]$CacheDirectory=(Join-Path $env:LOCALAPPDATA 'SideScreenBuild'))
$ErrorActionPreference='Stop'
$ProgressPreference='SilentlyContinue'
New-Item -ItemType Directory -Path $CacheDirectory -Force | Out-Null
$zig=Join-Path $CacheDirectory 'zig-x86_64-windows-0.14.1\zig.exe'
if(!(Test-Path -LiteralPath $zig)) {
    $archive=Join-Path $CacheDirectory 'zig-0.14.1.zip'
    Write-Host 'Downloading pinned Zig 0.14.1 (checksum verified before extraction).'
    Invoke-WebRequest -UseBasicParsing 'https://ziglang.org/download/0.14.1/zig-x86_64-windows-0.14.1.zip' -OutFile $archive -TimeoutSec 180
    if((Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash -ne '554F5378228923FFD558EAC35E21AF020C73789D87AFEABF4BFD16F2E6FEED2C'){throw 'Zig archive checksum mismatch'}
    Write-Host 'Extracting the compiler archive.'
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [IO.Compression.ZipFile]::ExtractToDirectory($archive,[IO.Path]::GetFullPath($CacheDirectory))
}
$minhook=Join-Path $CacheDirectory 'minhook-1.3.4';$revision='c3fcafdc10146beb5919319d0683e44e3c30d537'
if(!(Test-Path -LiteralPath $minhook)) {
    Write-Host 'Downloading pinned MinHook 1.3.4 source.'
    & git clone --branch v1.3.4 --depth 1 https://github.com/TsudaKageyu/minhook.git $minhook
    if($LASTEXITCODE -ne 0){throw 'MinHook download failed'}
}
if((& git -C $minhook rev-parse HEAD).Trim() -ne $revision){throw 'MinHook revision mismatch'}
if(@(& git -C $minhook status --porcelain).Count){throw 'MinHook checkout is modified'}
& (Join-Path $PSScriptRoot 'build-native.ps1') -ZigExecutable $zig -MinHookDirectory $minhook
