[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot;$dist=Join-Path $root 'dist'
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$wpf=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\WPF'
& $compiler /nologo /target:winexe /platform:x64 "/out:$dist\SideScreen.CuaWpfProbe.exe" "/r:$dist\SideScreen.Core.dll" /r:System.Drawing.dll /r:System.Web.Extensions.dll "/r:$wpf\PresentationFramework.dll" "/r:$wpf\PresentationCore.dll" "/r:$wpf\WindowsBase.dll" /r:System.Xaml.dll (Join-Path $PSScriptRoot 'CuaWpfProbe.cs')
if($LASTEXITCODE -ne 0){throw 'WPF probe build failed'}
& $compiler /nologo /target:winexe /platform:x64 "/out:$dist\SideScreen.BackgroundProbe.exe" "/r:$dist\SideScreen.Core.dll" /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Web.Extensions.dll (Join-Path $PSScriptRoot 'BackgroundProbe.cs')
if($LASTEXITCODE -ne 0){throw 'Native probe build failed'}
$checks=0;$processes=@();$files=@()
function Check($value,$message){if(!$value){throw $message};$script:checks++}
function Observe($handle,[switch]$Screenshot) {
    $result=& "$dist\agent.ps1" -Action CuaObserve -WindowHandle $handle -ExpectedDisplayId $script:display -NoScreenshot:(!$Screenshot) | ConvertFrom-Json
    Check $result.ok ('Observe failed: '+($result | ConvertTo-Json -Compress -Depth 4));$result
}
function Act($observation,$tool,$arguments) {
    & "$dist\agent.ps1" -Action CuaAct -WindowHandle $observation.windowHandle -ExpectedDisplayId $observation.displayId -ObservationId $observation.observationId -Tool $tool -ArgumentsJson ($arguments | ConvertTo-Json -Compress -Depth 5) | ConvertFrom-Json
}
function Token($observation,$label,$role) {
    $matches=@($observation.state.elements | Where-Object {(!$label -or $_.label -eq $label) -and (!$role -or $_.role -eq $role)})
    Check ($matches.Count -eq 1) 'Expected a unique observed token';$matches[0].element_token
}
function Quiet($result){Check $result.ok ('Action failed: '+($result | ConvertTo-Json -Compress -Depth 5));Check $result.focus.Preserved 'Foreground/keyboard focus changed'}
try {
    $script:display=(& "$dist\agent.ps1" -Action Status | ConvertFrom-Json).agentScreen.id
    Check ([bool]$display) 'Virtual display missing'
    foreach($kind in @('CuaWpf','Background')) {
        $state=Join-Path $env:TEMP ('sidescreen-cua-'+[guid]::NewGuid().ToString('N')+'.json');$files+=$state
        $process=Start-Process -FilePath "$dist\SideScreen.${kind}Probe.exe" -ArgumentList ('"'+$state+'"') -WindowStyle Hidden -PassThru;$processes+=$process
        $deadline=(Get-Date).AddSeconds(10)
        while(!(Test-Path -LiteralPath $state) -and (Get-Date) -lt $deadline){Start-Sleep -Milliseconds 100}
        Check (Test-Path -LiteralPath $state) 'Probe did not open'
        $handle=(Get-Content -LiteralPath $state -Raw -Encoding UTF8 | ConvertFrom-Json).handle
        $o=Observe $handle -Screenshot
        Check (Test-Path -LiteralPath $o.screenshotPath) 'CUA screenshot missing'
        Check ([bool]$o.state.capture_id) 'CUA capture binding missing'
        $edit=if($kind -eq 'Background'){Token $o 'Agent text' 'Edit'}else{Token $o '' 'Edit'}
        $text='CUA caf'+[char]0xE9+' '+[char]0x3A9
        $result=Act $o 'set_value' @{element_token=$edit;value=$text};Quiet $result
        $actual=(Get-Content $state -Raw -Encoding UTF8 | ConvertFrom-Json).text
        Check ($actual -eq $text) ('Unicode text did not reach '+$kind+' app: expected '+$text+'; received '+$actual+'; receipt '+($result | ConvertTo-Json -Compress -Depth 6))
        Check ($result.backend -eq $(if($kind -eq 'Background'){'native-control-messages'}else{'cua-driver'})) 'Unexpected text route'
        $again=Act $o 'set_value' @{element_token=$edit;value='duplicate'}
        Check (!$again.ok) 'Consumed observation was reused'
        $o=Observe $handle;$label=if($kind -eq 'Background'){'Increment counter'}else{'Increment WPF counter'}
        $result=Act $o 'click' @{element_token=(Token $o $label 'Button')};Quiet $result
        Check ((Get-Content $state -Raw -Encoding UTF8 | ConvertFrom-Json).clicks -eq 1) 'Button handler did not run exactly once'
        $o=Observe $handle;$label=if($kind -eq 'Background'){'Agent checkbox'}else{'WPF checkbox'}
        $result=Act $o 'click' @{element_token=(Token $o $label 'CheckBox')};Quiet $result
        Check ((Get-Content $state -Raw -Encoding UTF8 | ConvertFrom-Json).check) 'Checkbox did not change'
        $o=Observe $handle
        $result=Act $o 'click' @{element_token=(Token $o $(if($kind -eq 'Background'){'Increment counter'}else{'Increment WPF counter'}) 'Button');delivery_mode='foreground'}
        Check (!$result.ok) 'Foreground argument was accepted'
        Check ((Get-Content $state -Raw -Encoding UTF8 | ConvertFrom-Json).clicks -eq 1) 'Rejected override dispatched input'
        $o=Observe $handle -Screenshot;$result=Act $o 'click' @{x=-1;y=5}
        Check (!$result.ok) 'Out-of-image pixels were accepted'
        if($kind -eq 'Background') {
            foreach($label in @('Password','Read only')){$o=Observe $handle;$result=Act $o 'set_value' @{element_token=(Token $o $label 'Edit');value='forbidden'};Check (!$result.ok) 'Sensitive/readonly edit accepted'}
        }
        $wrong=& "$dist\agent.ps1" -Action CuaObserve -WindowHandle $handle -ExpectedDisplayId 'wrong-display' | ConvertFrom-Json
        Check (!$wrong.ok) 'Wrong display accepted'
    }
    Write-Host "PASS: $checks CUA integration checks (native + WPF, virtual display only)"
}finally {
    foreach($process in $processes){if(!$process.HasExited){$process.Kill();$process.WaitForExit()};$process.Dispose()}
    foreach($file in $files){if(Test-Path -LiteralPath $file){Remove-Item -LiteralPath $file -Force}}
}
