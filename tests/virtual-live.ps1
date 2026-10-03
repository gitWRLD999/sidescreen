[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot;$dist=Join-Path $root 'dist'
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
foreach($arch in @('x64','x86')) {
    & $compiler /nologo /target:winexe "/platform:$arch" "/out:$dist\SideScreen.VirtualProbe-$arch.exe" /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Web.Extensions.dll (Join-Path $PSScriptRoot 'VirtualProbe.cs') (Join-Path $root 'src\DisplayAudit.cs') (Join-Path $root 'src\WindowTools.cs')
    if($LASTEXITCODE -ne 0){throw 'Fixture build failed'}
}
$checks=0;$fixtures=@();$files=@();$worker=$null
function Check($value,$message){if(!$value){throw $message};$script:checks++}
function Call($request){$bytes=[Text.Encoding]::UTF8.GetBytes(($request|ConvertTo-Json -Depth 16 -Compress)+"`n");$worker.StandardInput.BaseStream.Write($bytes,0,$bytes.Length);$worker.StandardInput.BaseStream.Flush();$task=$worker.StandardOutput.ReadLineAsync();if(!$task.Wait(30000)){throw 'Worker timeout'};$task.Result|ConvertFrom-Json}
function Observe($h){$o=Call @{Action='CuaObserve';WindowHandle=$h;ExpectedDisplayId=$script:display;IncludeScreenshot=$true};Check $o.ok ('Observe failed: '+($o|ConvertTo-Json -Depth 4 -Compress));$o}
function Act($o,$tool,$arg){Call @{Action='VirtualAct';WindowHandle=$o.windowHandle;ExpectedDisplayId=$o.displayId;ObservationId=$o.observationId;CursorId=$script:cursor;CursorLabel='Native test';Tool=$tool;Arguments=$arg}}
function Quiet($r){Check $r.ok ('Input failed: '+($r|ConvertTo-Json -Depth 7 -Compress));Check $r.focus.Preserved 'Focus changed';if(!$r.focus.CursorPreserved){Write-Host ('Pointer movement observed: '+($r.focus|ConvertTo-Json -Compress))}}
function State{try{Get-Content -LiteralPath $script:file -Raw -Encoding UTF8|ConvertFrom-Json}catch{Start-Sleep -Milliseconds 60;Get-Content -LiteralPath $script:file -Raw -Encoding UTF8|ConvertFrom-Json}}
function Point($o,$sx,$sy){$saved=Get-Content -LiteralPath (Join-Path $env:USERPROFILE ('AgentTools\SideScreen\state\cua-observations\'+$o.observationId+'.json')) -Raw|ConvertFrom-Json;Check $saved.HasCaptureTransform 'Capture transform unavailable';@{x=($sx-$saved.CaptureOriginX)/$saved.CaptureScaleX;y=($sy-$saved.CaptureOriginY)/$saved.CaptureScaleY}}
function Token($o,$label){$e=@($o.state.elements|Where-Object label -eq $label);Check ($e.Count -eq 1) ('Control ambiguous '+$label);$e[0].element_token}
try {
    $info=New-Object Diagnostics.ProcessStartInfo "$dist\SideScreen.Cua.exe",'--server';$info.UseShellExecute=$false;$info.CreateNoWindow=$true;$info.RedirectStandardInput=$true;$info.RedirectStandardOutput=$true;$info.StandardOutputEncoding=[Text.UTF8Encoding]::new($false)
    $worker=[Diagnostics.Process]::Start($info);$status=Call @{Action='Status'};$script:display=$status.agentScreen.id;Check ([bool]$display) 'SideScreen missing';Check $status.virtualInputInstalled 'Native adapter missing'
    foreach($arch in @('x64','x86')) {
        $script:cursor=[guid]::NewGuid().ToString('N');$script:file=Join-Path $env:TEMP ('sidescreen-virtual-'+[guid]::NewGuid().ToString('N')+'.json');$files+=$file
        $fixture=Start-Process -FilePath "$dist\SideScreen.VirtualProbe-$arch.exe" -ArgumentList ('"'+$file+'"') -WindowStyle Hidden -PassThru;$fixtures+=$fixture
        $deadline=(Get-Date).AddSeconds(10);while(!(Test-Path -LiteralPath $file) -and (Get-Date) -lt $deadline){Start-Sleep -Milliseconds 100};Check (Test-Path -LiteralPath $file) 'Fixture failed to open';$h=(State).handle
        $o=Observe $h;$state=State;$p=Point $o $state.buttonCenter.x $state.buttonCenter.y;$r=Act $o 'click' $p;Quiet $r
        Start-Sleep -Milliseconds 75;$s=State;Check ($s.clicks -eq 1) 'Raw standard button did not click';Check ($s.pointer.x -eq $state.buttonCenter.x -and $s.pointer.y -eq $state.buttonCenter.y) 'App did not see virtual cursor';Check ($r.blockedActivationAttempts -ge 2) 'Activation/cursor attempts not blocked'
        $again=Act $o 'click' $p;Check (!$again.ok) 'Observation replayed'
        $o=Observe $h;$text='Unicode caf'+[char]0xe9+' '+[char]0x3a9;$r=Act $o 'type' @{element_token=(Token $o 'Virtual text');text=$text};Quiet $r;Check ((State).text -eq $text) ('Unicode typing effect missing: '+((State)|ConvertTo-Json -Depth 5 -Compress))
        $o=Observe $h;$r=Act $o 'press' @{element_token=(Token $o 'Virtual text');key='A';modifiers=@('Control')};Quiet $r;Check ((State).controlKey -and (State).asyncControl) 'Control modifier/query not virtualized'
        $o=Observe $h;$r=Act $o 'type' @{element_token=(Token $o 'Virtual text');text='replacement'};Quiet $r;Check ((State).text -eq 'replacement') 'Ctrl+A selection effect missing'
        $o=Observe $h;$r=Act $o 'press' @{element_token=(Token $o 'Virtual text');key='B';modifiers=@('Shift')};Quiet $r;Check ((State).shiftKey -and (State).asyncShift -and !(State).controlKey -and !(State).asyncControl) 'Modifier state leaked across events'
        foreach($label in @('Password','Read only')){$o=Observe $h;$r=Act $o 'type' @{element_token=(Token $o $label);text='forbidden'};Check (!$r.ok) 'Sensitive edit accepted'}
        $o=Observe $h;$s=State;$points=@((Point $o ($s.canvasOrigin.x+40) ($s.canvasOrigin.y+50)),(Point $o ($s.canvasOrigin.x+160) ($s.canvasOrigin.y+50)),(Point $o ($s.canvasOrigin.x+260) ($s.canvasOrigin.y+70)));$r=Act $o 'drag' @{points=$points;duration_ms=100};Quiet $r;Start-Sleep -Milliseconds 75;$s=State;Check ($s.down -eq 1 -and $s.up -eq 1 -and $s.dragMoves -ge 2 -and $s.last.x -eq 260) 'Drag handlers/capture missing'
        $o=Observe $h;$p=Point $o ($s.canvasOrigin.x+80) ($s.canvasOrigin.y+80);$r=Act $o 'scroll' ($p+@{delta=120});Quiet $r;Start-Sleep -Milliseconds 75;Check ((State).wheel -eq 120) 'Wheel handler missing'
        $o=Observe $h;$r=Act $o 'scroll' ($p+@{delta=-120;axis='horizontal'});Quiet $r;Start-Sleep -Milliseconds 75;Check ((State).horizontal -eq -120) 'Horizontal wheel missing'
        $o=Observe $h;$r=Act $o 'click' ($p+@{count=2});Quiet $r;Start-Sleep -Milliseconds 75;Check ((State).doubleClicks -eq 1) 'Double-click handler missing'
        $o=Observe $h;$r=Act $o 'drag' @{points=@($p,@{x=-1;y=0})};Check (!$r.ok) 'Escaping drag accepted'
        $scope=Call @{Action='Scope';WindowHandle=$h;ExpectedDisplayId=$display};$release=Call @{Action='VirtualRelease';WindowHandle=$h;ExpectedDisplayId=$display;ExpectedProcessId=$scope.processId;ExpectedProcessStartTicks=$scope.processStartTicks};Check $release.ok 'Virtual capture/focus release failed'
    }
    Write-Host "PASS: $checks virtual input checks, x64 + x86 real app handlers"
}finally {
    if($worker){$worker.StandardInput.Close();if(!$worker.WaitForExit(3000)){$worker.Kill()};$worker.Dispose()}
    foreach($fixture in $fixtures){if(!$fixture.HasExited){$fixture.Kill();$fixture.WaitForExit()};$fixture.Dispose()}
    foreach($path in $files){if(Test-Path -LiteralPath $path){Remove-Item -LiteralPath $path -Force}}
}
