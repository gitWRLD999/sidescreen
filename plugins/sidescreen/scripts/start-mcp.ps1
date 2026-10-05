[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
try {
    if((Get-Process -Id $PID).SessionId -eq 0){throw 'SideScreen needs the signed-in interactive Windows desktop, not a services session.'}
    $cli=Join-Path (Split-Path $PSScriptRoot) 'runtime\server.cjs'
    if(!(Test-Path -LiteralPath $cli)){throw 'Missing bundled SideScreen desktop server; extract the complete plugin package.'}
    $node=(Get-Command node.exe -ErrorAction Stop).Source
    $side=if($env:SIDESCREEN_HOME){[IO.Path]::GetFullPath($env:SIDESCREEN_HOME)}else{Join-Path (Split-Path $PSScriptRoot) 'native'}
    $env:SIDESCREEN_HOME=$side
    $supervisor=Join-Path $side 'start-cua.ps1'
    if(!(Test-Path -LiteralPath (Join-Path $side 'SideScreen.Cua.exe'))){throw 'Missing bundled SideScreen native helper; extract the complete package.'}
    $binary=Join-Path $env:USERPROFILE 'AgentTools\Cua\bin\cua-driver.exe'
    # A mutex makes this idempotent. An explicit pause remains authoritative.
    if((Test-Path -LiteralPath $supervisor) -and ((Test-Path -LiteralPath $binary) -or $env:SIDESCREEN_CUA_BINARY)) {
        Start-Process powershell.exe -ArgumentList ('-NoProfile -ExecutionPolicy Bypass -File "'+$supervisor+'"') -WindowStyle Hidden
    }
    # The server has its own stdio connection. No network broker or browser
    # profile manager is required or launched.
    & $node $cli
    exit $LASTEXITCODE
}catch {
    [Console]::Error.WriteLine($_.Exception.Message)
    exit 1
}
