[CmdletBinding()]
param(
    [string]$StateDirectory,
    [ValidateRange(100,30000)][int]$ProbeTimeoutMs=5000,
    [ValidateRange(100,60000)][int]$RetryDelayMs=10000
)
$ErrorActionPreference='Stop'
$binary=if($env:SIDESCREEN_CUA_BINARY){$env:SIDESCREEN_CUA_BINARY}else{Join-Path $env:LOCALAPPDATA 'Programs\Cua\cua-driver\bin\cua-driver.exe'}
$sharedBinary=Join-Path $env:USERPROFILE 'AgentTools\Cua\bin\cua-driver.exe'
if(!$env:SIDESCREEN_CUA_BINARY -and (Test-Path -LiteralPath $sharedBinary)){$binary=$sharedBinary}
if(!(Test-Path -LiteralPath $binary)){throw 'Install the official optional CUA Driver before starting this supervisor.'}
$session=(Get-Process -Id $PID).SessionId
if($session -eq 0){throw 'Start SideScreen CUA from the signed-in interactive desktop, not a service/SSH session.'}
$state=if($StateDirectory){[IO.Path]::GetFullPath($StateDirectory)}else{Join-Path $env:USERPROFILE 'AgentTools\SideScreen\state\cua-service'}
New-Item -ItemType Directory -Path $state -Force | Out-Null
$mutexName="Local\SideScreen.CuaSupervisor.$session"
if($StateDirectory){$hash=[Security.Cryptography.SHA256]::Create();try{$mutexName+='.'+([BitConverter]::ToString($hash.ComputeHash([Text.Encoding]::UTF8.GetBytes($state))).Replace('-','').Substring(0,16))}finally{$hash.Dispose()}}
$mutex=New-Object Threading.Mutex($false,$mutexName)
if(!$mutex.WaitOne(0)){exit 0}
function Health([string]$status,[string]$message='',[int]$servicePid=0) {
    $record=@{status=$status;sessionId=$session;supervisorPid=$PID;servicePid=$servicePid;updatedUtc=[DateTime]::UtcNow.ToString('o');error=$message}
    $pending=Join-Path $state 'health.pending.json'
    $record | ConvertTo-Json -Compress | Set-Content -LiteralPath $pending -Encoding UTF8
    Move-Item -LiteralPath $pending -Destination (Join-Path $state 'health.json') -Force
}
function Delay([int]$duration) {
    $deadline=[DateTime]::UtcNow.AddMilliseconds($duration)
    while([DateTime]::UtcNow -lt $deadline -and !(Test-Path -LiteralPath (Join-Path $state 'paused'))) {Start-Sleep -Milliseconds 100}
}
try {
    $env:CUA_DRIVER_RS_TELEMETRY_ENABLED='false'
    $socket="\\.\pipe\SideScreen.Cua.$session"
    while(!(Test-Path -LiteralPath (Join-Path $state 'paused'))) {
      try {
        Health 'checking'
        # Reuse a live service rather than competing for its pipe or killing it.
        $probeInfo=New-Object Diagnostics.ProcessStartInfo
        $probeInfo.FileName=$binary;$probeInfo.Arguments="status --socket $socket"
        $probeInfo.UseShellExecute=$false;$probeInfo.CreateNoWindow=$true
        $probeInfo.RedirectStandardOutput=$true;$probeInfo.RedirectStandardError=$true
        $probe=[Diagnostics.Process]::Start($probeInfo)
        try {
            $probeOutput=$probe.StandardOutput.ReadToEndAsync();$probeError=$probe.StandardError.ReadToEndAsync()
            if(!$probe.WaitForExit($ProbeTimeoutMs)){$probe.Kill();throw 'CUA status timed out; service state unknown. Waiting without starting another daemon.'}
            $live=($probe.ExitCode -eq 0)
        }finally{$probe.Dispose()}
        if($live){Health 'running';Delay 15000;continue}
        Health 'starting'
        $start=New-Object Diagnostics.ProcessStartInfo
        $start.FileName=$binary;$start.Arguments="serve --socket $socket --cursor-reduced-motion on"
        $start.UseShellExecute=$false;$start.CreateNoWindow=$true
        $start.RedirectStandardOutput=$true;$start.RedirectStandardError=$true
        $process=[Diagnostics.Process]::Start($start)
        try {
            Health 'running' '' $process.Id
            $output=$process.StandardOutput.ReadToEndAsync();$errors=$process.StandardError.ReadToEndAsync()
            while(!$process.WaitForExit(1000)) {
                if(Test-Path -LiteralPath (Join-Path $state 'paused')){$process.Kill();break}
                Health 'running' '' $process.Id
            }
            $log=$errors.Result+$output.Result
            if($log.Length -gt 16384){$log=$log.Substring($log.Length-16384)}
            Set-Content -LiteralPath (Join-Path $state 'last-exit.log') -Value $log
        }finally{$process.Dispose()}
        Health 'waiting' 'Owned CUA daemon exited. Input is never replayed.'
      }catch {
        # A transient status/launch error must not silently remove durability.
        Health 'waiting' $_.Exception.Message
      }
        Delay $RetryDelayMs
    }
    Health 'paused'
}finally{$mutex.ReleaseMutex();$mutex.Dispose()}
