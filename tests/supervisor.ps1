[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
$testState=Join-Path $env:TEMP ('SideScreen-supervisor-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testState -Force | Out-Null
$fake=Join-Path $testState 'FakeCua.exe'
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$source=Join-Path $testState 'FakeCua.cs'
@'
using System;using System.IO;using System.Threading;
class FakeCua {
  static int Main(string[] args) {
    var p=Environment.GetEnvironmentVariable("SIDESCREEN_FAKE_STATE");
    if(args[0]=="status") {
      var once=Path.Combine(p,"timed-out");
      if(!File.Exists(once)){File.WriteAllText(once,"");Thread.Sleep(10000);}
      return 1;
    }
    var count=Path.Combine(p,"starts");
    File.WriteAllText(count,(File.Exists(count)?int.Parse(File.ReadAllText(count))+1:1).ToString());
    while(!File.Exists(Path.Combine(p,"exit-daemon")))Thread.Sleep(30);
    File.Delete(Path.Combine(p,"exit-daemon"));return 1;
  }
}
'@ | Set-Content -LiteralPath $source
& $compiler /nologo /target:exe "/out:$fake" $source
if($LASTEXITCODE -ne 0){throw 'Fake CUA build failed'}
$priorBinary=$env:SIDESCREEN_CUA_BINARY;$priorState=$env:SIDESCREEN_FAKE_STATE
$checks=0;$supervisor=$null
function Check($condition,$label){if(!$condition){throw $label};$script:checks++}
function Until([scriptblock]$predicate,[string]$label) {
    $deadline=[DateTime]::UtcNow.AddSeconds(8)
    while([DateTime]::UtcNow -lt $deadline){if(& $predicate){$script:checks++;return};Start-Sleep -Milliseconds 80}
    throw $label
}
try {
    $env:SIDESCREEN_CUA_BINARY=$fake;$env:SIDESCREEN_FAKE_STATE=$testState
    $script=Join-Path (Split-Path $PSScriptRoot) 'scripts\start-cua.ps1'
    $arguments='-NoProfile -ExecutionPolicy Bypass -File "'+$script+'" -StateDirectory "'+$testState+'" -ProbeTimeoutMs 200 -RetryDelayMs 500'
    $supervisor=Start-Process powershell.exe -ArgumentList $arguments -WindowStyle Hidden -PassThru
    Until {Test-Path -LiteralPath (Join-Path $testState 'timed-out')} 'Probe never ran'
    Until { $h=Join-Path $testState 'health.json'; (Test-Path $h) -and (Get-Content $h -Raw | ConvertFrom-Json).error -like '*timed out*' } 'Timeout was not reported'
    Check (!$supervisor.HasExited) 'Supervisor exited on transient status timeout'
    Until { $p=Join-Path $testState 'starts';(Test-Path $p) -and (Get-Content $p -Raw).Trim() -eq '1' } 'Daemon did not start after recovery'
    Set-Content -LiteralPath (Join-Path $testState 'exit-daemon') -Value ''
    Until {(Get-Content (Join-Path $testState 'starts') -Raw).Trim() -eq '2'} 'Daemon was not restarted'
    Set-Content -LiteralPath (Join-Path $testState 'paused') -Value ''
    Check ($supervisor.WaitForExit(5000)) 'Pause did not stop supervisor'
    Check ((Get-Content (Join-Path $testState 'health.json') -Raw | ConvertFrom-Json).status -eq 'paused') 'Pause health missing'
    Write-Host "PASS: $checks supervisor checks (timeout, recovery, restart, pause)"
}finally {
    if($supervisor -and !$supervisor.HasExited){$supervisor.Kill()}
    $env:SIDESCREEN_CUA_BINARY=$priorBinary;$env:SIDESCREEN_FAKE_STATE=$priorState
}
