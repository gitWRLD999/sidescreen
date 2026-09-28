[CmdletBinding()]
param(
    [ValidateSet('Status','Windows','Candidates','Move','Capture')][string]$Action='Status',
    [long]$WindowHandle,
    [ValidateSet('Agent','Main')][string]$Destination='Agent',
    [string]$ExpectedDisplayId,
    [string]$OutputPath
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
            @{ok=$true;available=($virtual.Count -eq 1);agentScreen=$screen;physicalScreens=$physicalScreens;inputIsolation=$false;version='0.1.1'} | ConvertTo-Json -Depth 5
        }
        'Windows' { @{ok=$true;scope='agent screen';windows=@([SideScreen.Windows]::ListOnAgentDisplay())} | ConvertTo-Json -Depth 6 }
        'Candidates' { @{ok=$true;scope='all visible windows';windows=@([SideScreen.Windows]::List())} | ConvertTo-Json -Depth 6 }
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
