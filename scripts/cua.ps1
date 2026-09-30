[CmdletBinding()]
param(
    [ValidateSet('Status','Windows','CuaObserve','CuaAct')][string]$Action='Status',
    [long]$WindowHandle,[string]$ExpectedDisplayId,[string]$ObservationId,
    [ValidateSet('click','set_value','type_text','scroll','press_key','hotkey')][string]$Tool,
    [string]$ArgumentsJson='{}',[switch]$NoScreenshot
)
$ErrorActionPreference='Stop'
try {
    $request=@{Action=$Action;WindowHandle=$WindowHandle;ExpectedDisplayId=$ExpectedDisplayId;ObservationId=$ObservationId;Tool=$Tool;Arguments=($ArgumentsJson | ConvertFrom-Json);IncludeScreenshot=(!$NoScreenshot)}
    $start=New-Object Diagnostics.ProcessStartInfo
    $start.FileName=Join-Path $PSScriptRoot 'SideScreen.Cua.exe'
    $start.UseShellExecute=$false
    $start.CreateNoWindow=$true
    $start.RedirectStandardInput=$true
    $start.RedirectStandardOutput=$true
    $start.RedirectStandardError=$true
    $start.StandardOutputEncoding=New-Object Text.UTF8Encoding($false)
    $start.StandardErrorEncoding=New-Object Text.UTF8Encoding($false)
    $process=[Diagnostics.Process]::Start($start)
    try {
        $output=$process.StandardOutput.ReadToEndAsync();$errors=$process.StandardError.ReadToEndAsync()
        $bytes=[Text.Encoding]::UTF8.GetBytes(($request | ConvertTo-Json -Depth 8 -Compress))
        $process.StandardInput.BaseStream.Write($bytes,0,$bytes.Length);$process.StandardInput.Close()
        if(!$process.WaitForExit(30000)){$process.Kill();throw 'CUA bridge timed out. Outcome unknown; observe before continuing, never retry automatically.'}
        if(!$output.Result){throw ('CUA bridge failed: '+$errors.Result)}
        Write-Output $output.Result.Trim()
        exit $process.ExitCode
    }finally{$process.Dispose()}
}catch{@{ok=$false;stop=$true;error=$_.Exception.Message} | ConvertTo-Json -Compress;exit 1}
