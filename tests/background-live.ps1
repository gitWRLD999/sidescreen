[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot
$dist=Join-Path $root 'dist'
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $compiler /nologo /target:winexe /platform:x64 "/out:$dist\SideScreen.BackgroundProbe.exe" "/r:$dist\SideScreen.Core.dll" /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Web.Extensions.dll (Join-Path $PSScriptRoot 'BackgroundProbe.cs')
if($LASTEXITCODE -ne 0){throw 'Probe build failed'}
$state=Join-Path $env:TEMP ('sidescreen-probe-'+[guid]::NewGuid().ToString('N')+'.json')
$probe=Start-Process -FilePath (Join-Path $dist 'SideScreen.BackgroundProbe.exe') -ArgumentList ('"'+$state+'"') -WindowStyle Hidden -PassThru
$checks=0
function Check($condition,$message){if(!$condition){throw $message};$script:checks++}
function Inspect { & "$dist\agent.ps1" -Action Inspect -WindowHandle $script:handle -ExpectedDisplayId $script:displayId | ConvertFrom-Json }
function Act($inspection,$name,$operation,$value) {
    Check $inspection.ok ('Inspection failed: '+$inspection.error)
    $elements=@($inspection.observation.Elements | Where-Object Name -eq $name)
    Check ($elements.Count -eq 1) ('Expected unique element: '+$name)
    Check ($elements[0].Actions -contains $operation) ('Unsupported probe control: '+($elements[0] | ConvertTo-Json -Compress))
    $parameters=@{Action='Act';WindowHandle=$script:handle;ExpectedDisplayId=$script:displayId;ObservationId=$inspection.observation.Id;ElementId=$elements[0].Id;Operation=$operation}
    if($null -ne $value){$parameters.Value=$value}
    $result=& "$dist\agent.ps1" @parameters | ConvertFrom-Json
    Check $result.ok ('Action '+$operation+' on '+$name+' failed: '+($result | ConvertTo-Json -Compress -Depth 5))
    Check $result.focus.Preserved 'Focus/cursor changed during background action'
    $result
}
try {
    for($attempt=0;$attempt -lt 40 -and !(Test-Path $state);$attempt++){Start-Sleep -Milliseconds 100}
    $script:handle=(Get-Content $state -Raw | ConvertFrom-Json).handle
    $status=& "$dist\agent.ps1" -Action Status | ConvertFrom-Json
    $script:displayId=$status.agentScreen.id
    $inspection=Inspect
    foreach($name in @('Read only','Password','Custom checkbox')) {
        $blocked=@($inspection.observation.Elements | Where-Object Name -eq $name)
        Check ($blocked.Count -eq 1 -and $blocked[0].Actions.Count -eq 0) ('Unsafe or unsupported control advertised: '+$name)
    }
    $unicode='Background Unicode: caf'+[char]0xe9+' '+[char]0x3a9
    Act $inspection 'Agent text' 'SetValue' $unicode | Out-Null
    Check ((Get-Content $state -Raw -Encoding UTF8 | ConvertFrom-Json).text -eq $unicode) 'Target did not receive Unicode text'
    $reused=& "$dist\background.ps1" -Action Act -WindowHandle $handle -ExpectedDisplayId $displayId -ObservationId $inspection.observation.Id -ElementId 0 -Operation Invoke | ConvertFrom-Json
    Check (!$reused.ok) 'Consumed observation was accepted'
    Act (Inspect) 'Increment counter' 'Invoke' $null | Out-Null
    Check ((Get-Content $state -Raw | ConvertFrom-Json).clicks -eq 1) 'Button was not invoked exactly once'
    Act (Inspect) 'Agent checkbox' 'Toggle' $null | Out-Null
    Check (Get-Content $state -Raw | ConvertFrom-Json).check 'Checkbox did not change'
    Act (Inspect) 'Second choice' 'Select' $null | Out-Null
    Check ((Get-Content $state -Raw | ConvertFrom-Json).selection -eq 1) 'Selection did not change'
    Act (Inspect) 'Agent text' 'SetValue' '' | Out-Null
    Check ((Get-Content $state -Raw | ConvertFrom-Json).text -eq '') 'Empty value did not clear the text field'
    $wrong=& "$dist\background.ps1" -Action Inspect -WindowHandle $handle -ExpectedDisplayId 'invalid-display' | ConvertFrom-Json
    Check (!$wrong.ok) 'Wrong display accepted'
    $inspection=Inspect
    $unsupported=& "$dist\background.ps1" -Action Act -WindowHandle $handle -ExpectedDisplayId $displayId -ObservationId $inspection.observation.Id -ElementId 0 -Operation SetValue -Value 'unsupported' | ConvertFrom-Json
    Check (!$unsupported.ok) 'Unsupported pattern accepted'
    $candidates=& "$dist\agent.ps1" -Action Candidates | ConvertFrom-Json
    $primary=@($candidates.windows | Where-Object { $_.Bounds.Right -le $status.agentScreen.x -and $_.Bounds.Width -gt 0 })
    if($primary.Count){
        $outside=& "$dist\background.ps1" -Action Inspect -WindowHandle $primary[0].Handle -ExpectedDisplayId $displayId | ConvertFrom-Json
        Check (!$outside.ok) 'Primary-screen window was accepted'
    }
    Write-Host "PASS: $checks background input checks; value, invoke, toggle, select, focus and refusal paths"
} catch {
    if(Test-Path $state){Write-Host ('Probe state: '+(Get-Content $state -Raw))}
    throw
} finally {
    if(!$probe.HasExited){$probe.Kill();$probe.WaitForExit()}
    $probe.Dispose()
    if(Test-Path -LiteralPath $state){Remove-Item -LiteralPath $state}
}
