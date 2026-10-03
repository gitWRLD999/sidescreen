[CmdletBinding()]
param(
    [ValidateSet('Status','Windows','Candidates','Move','Capture','Inspect','Act','CuaObserve','CuaAct')][string]$Action='Status',
    [long]$WindowHandle,
    [ValidateSet('Agent','Main')][string]$Destination='Agent',
    [string]$ExpectedDisplayId,
    [string]$OutputPath,
    [string]$ObservationId,
    [int]$ElementId=-1,
    [ValidateSet('Invoke','SetValue','Toggle','Select')][string]$Operation,
    [AllowEmptyString()][string]$Value,
    [ValidateSet('click','set_value','type_text','scroll','press_key','hotkey')][string]$Tool,
    [string]$ArgumentsJson='{}',
    [switch]$NoScreenshot
)
$ErrorActionPreference='Stop'
try {
    Add-Type -Path (Join-Path $PSScriptRoot 'SideScreen.Core.dll')
    switch($Action) {
        'Status' {
            $displays=@([DisplayAudit]::Read())
            $virtual=@($displays | Where-Object IsVirtualMonitor)
            $physical=@($displays | Where-Object IsUsableMainDisplay)
            $screen=$null
            if($virtual.Count -eq 1){
                $agent=$virtual[0]
                $screen=@{id=[SideScreen.Layout]::Id($agent);deviceName=$agent.GdiName;hardwareId=$agent.MonitorHardwareId;x=$agent.X;y=$agent.Y;width=$agent.Width;height=$agent.Height}
            }
            $physicalScreens=@($physical | ForEach-Object { @{id=[SideScreen.Layout]::Id($_);deviceName=$_.GdiName;x=$_.X;y=$_.Y;width=$_.Width;height=$_.Height} })
            $cua=& (Join-Path $PSScriptRoot 'cua.ps1') -Action Status | ConvertFrom-Json
            @{ok=$true;available=$cua.available;agentScreen=$screen;physicalScreens=$physicalScreens;inputIsolation=$false;cua=$cua;backgroundInput=@{backend='native-control-messages';operations=@('SetValue','Invoke','Toggle','Select');requiresInspection=$true;globalInputFallback=$false};version='0.7.0'} | ConvertTo-Json -Depth 5
        }
        'Windows' { @{ok=$true;scope='agent screen';windows=@([SideScreen.Windows]::ListOnAgentDisplay())} | ConvertTo-Json -Depth 6 }
        'Candidates' { @{ok=$true;scope='all visible windows';windows=@([SideScreen.Windows]::List())} | ConvertTo-Json -Depth 6 }
        {$_ -eq 'CuaObserve' -or $_ -eq 'CuaAct'} {
            $parameters=@{Action=$Action;WindowHandle=$WindowHandle;ExpectedDisplayId=$ExpectedDisplayId;ObservationId=$ObservationId;ArgumentsJson=$ArgumentsJson;NoScreenshot=$NoScreenshot}
            if($Tool){$parameters.Tool=$Tool}
            & (Join-Path $PSScriptRoot 'cua.ps1') @parameters
            exit $LASTEXITCODE
        }
        {$_ -eq 'Inspect' -or $_ -eq 'Act'} {
            if(!$WindowHandle -or !$ExpectedDisplayId){throw 'Provide WindowHandle and ExpectedDisplayId from fresh Windows/Status results.'}
            $parameters=@{Action=$Action;WindowHandle=$WindowHandle;ExpectedDisplayId=$ExpectedDisplayId;ObservationId=$ObservationId;ElementId=$ElementId}
            if($Operation){$parameters.Operation=$Operation}
            if($PSBoundParameters.ContainsKey('Value')){$parameters.Value=$Value}
            & (Join-Path $PSScriptRoot 'background.ps1') @parameters
            exit $LASTEXITCODE
        }
        'Move' {
            if(!$WindowHandle){throw 'Provide -WindowHandle from a fresh Windows listing.'}
            if(!$ExpectedDisplayId){throw 'Provide -ExpectedDisplayId from a fresh Status result.'}
            $receipt=[SideScreen.Windows]::Move($WindowHandle,($Destination -eq 'Agent'),$ExpectedDisplayId)
            @{ok=$true;receipt=$receipt} | ConvertTo-Json -Depth 6
            if(!$receipt.FocusPreserved -or !$receipt.CursorPreserved){exit 2}
        }
        'Capture' {
            if(!$OutputPath){throw 'Provide an explicit -OutputPath for the PNG.'}
            if(!$ExpectedDisplayId){throw 'Provide -ExpectedDisplayId from a fresh Status result.'}
            $capturePath=[IO.Path]::GetFullPath($OutputPath)
            if(Test-Path -LiteralPath $capturePath){throw 'Output already exists; choose a new filename.'}
            [SideScreen.Windows]::Capture($capturePath,$ExpectedDisplayId)
            @{ok=$true;path=$capturePath} | ConvertTo-Json
        }
    }
} catch {
    @{ok=$false;error=$_.Exception.Message} | ConvertTo-Json -Compress
    exit 1
}
