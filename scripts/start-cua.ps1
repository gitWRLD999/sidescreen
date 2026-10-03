[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
$binary=if($env:SIDESCREEN_CUA_BINARY){$env:SIDESCREEN_CUA_BINARY}else{Join-Path $env:LOCALAPPDATA 'Programs\Cua\cua-driver\bin\cua-driver.exe'}
$sharedBinary=Join-Path $env:USERPROFILE 'AgentTools\Cua\bin\cua-driver.exe'
if(!$env:SIDESCREEN_CUA_BINARY -and (Test-Path -LiteralPath $sharedBinary)){$binary=$sharedBinary}
if(!(Test-Path -LiteralPath $binary)){throw 'Install the official optional CUA Driver before starting this supervisor.'}
$session=(Get-Process -Id $PID).SessionId
if($session -eq 0){throw 'Start SideScreen CUA from the signed-in interactive desktop, not a service/SSH session.'}
$state=Join-Path $env:USERPROFILE 'AgentTools\SideScreen\state\cua-service'
New-Item -ItemType Directory -Path $state -Force | Out-Null
$mutex=New-Object Threading.Mutex($false,"Local\SideScreen.CuaSupervisor.$session")
if(!$mutex.WaitOne(0)){exit 0}
try {
    $env:CUA_DRIVER_RS_TELEMETRY_ENABLED='false'
    $socket="\\.\pipe\SideScreen.Cua.$session"
    while(!(Test-Path -LiteralPath (Join-Path $state 'paused'))) {
        # Reuse a live service rather than competing for its pipe or killing it.
        $probeInfo=New-Object Diagnostics.ProcessStartInfo
        $probeInfo.FileName=$binary;$probeInfo.Arguments="status --socket $socket"
        $probeInfo.UseShellExecute=$false;$probeInfo.CreateNoWindow=$true
        $probeInfo.RedirectStandardOutput=$true;$probeInfo.RedirectStandardError=$true
        $probe=[Diagnostics.Process]::Start($probeInfo)
        try {
            $probeOutput=$probe.StandardOutput.ReadToEndAsync();$probeError=$probe.StandardError.ReadToEndAsync()
            if(!$probe.WaitForExit(5000)){$probe.Kill();throw 'CUA status timed out.'}
            $live=($probe.ExitCode -eq 0)
        }finally{$probe.Dispose()}
        if($live){Start-Sleep -Seconds 15;continue}
        $start=New-Object Diagnostics.ProcessStartInfo
        $start.FileName=$binary;$start.Arguments="serve --socket $socket --cursor-reduced-motion on"
        $start.UseShellExecute=$false;$start.CreateNoWindow=$true
        $start.RedirectStandardOutput=$true;$start.RedirectStandardError=$true
        $process=[Diagnostics.Process]::Start($start)
        try {
            $output=$process.StandardOutput.ReadToEndAsync();$errors=$process.StandardError.ReadToEndAsync()
            while(!$process.WaitForExit(1000)) {
                if(Test-Path -LiteralPath (Join-Path $state 'paused')){$process.Kill();break}
            }
            Set-Content -LiteralPath (Join-Path $state 'last-exit.log') -Value ($errors.Result+$output.Result)
        }finally{$process.Dispose()}
        Start-Sleep -Seconds 10
    }
}finally{$mutex.ReleaseMutex();$mutex.Dispose()}
