[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$ZigExecutable,[Parameter(Mandatory=$true)][string]$MinHookDirectory)
$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
$output=Join-Path $root 'dist'
New-Item -ItemType Directory -Path $output -Force | Out-Null
$minhook=[IO.Path]::GetFullPath($MinHookDirectory)
$sources=@((Join-Path $PSScriptRoot 'virtual-input.c'),(Join-Path $minhook 'src\buffer.c'),(Join-Path $minhook 'src\hook.c'),(Join-Path $minhook 'src\trampoline.c'))
foreach($arch in @('x86_64','x86')) {
    Write-Host "Compiling the $arch adapter."
    $hde=if($arch -eq 'x86_64'){'hde64.c'}else{'hde32.c'}
    $name=if($arch -eq 'x86_64'){'SideScreen.VirtualInput.dll'}else{'SideScreen.VirtualInput32.dll'}
    & $ZigExecutable cc -target "$arch-windows-gnu" -O2 -shared -DUNICODE -D_UNICODE -I (Join-Path $minhook 'include') @sources (Join-Path $minhook ('src\hde\'+$hde)) -luser32 -lkernel32 -lole32 -o (Join-Path $output $name)
    if($LASTEXITCODE -ne 0){throw "Native $arch build failed"}
}
Copy-Item -LiteralPath (Join-Path $minhook 'LICENSE.txt') -Destination (Join-Path $output 'MinHook-LICENSE.txt') -Force
Write-Host 'Built both original virtual-input adapters; MinHook license copied.'
