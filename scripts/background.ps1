[CmdletBinding()]
param(
    [ValidateSet('Inspect','Act')][string]$Action='Inspect',
    [Parameter(Mandatory=$true)][long]$WindowHandle,
    [Parameter(Mandatory=$true)][string]$ExpectedDisplayId,
    [string]$ObservationId,
    [int]$ElementId=-1,
    [ValidateSet('Invoke','SetValue','Toggle','Select')][string]$Operation,
    [AllowEmptyString()][string]$Value
)
$ErrorActionPreference='Stop'
try {
    $request=@{Action=$Action;WindowHandle=$WindowHandle;ExpectedDisplayId=$ExpectedDisplayId;ObservationId=$ObservationId;ElementId=$ElementId;Operation=$Operation}
    if($PSBoundParameters.ContainsKey('Value')){$request.Value=$Value}
    $start=New-Object Diagnostics.ProcessStartInfo
    $start.FileName=Join-Path $PSScriptRoot 'SideScreen.Input.exe'
    $start.UseShellExecute=$false
    $start.CreateNoWindow=$true
    $start.RedirectStandardInput=$true
    $start.RedirectStandardOutput=$true
    $start.RedirectStandardError=$true
    $start.StandardOutputEncoding=New-Object Text.UTF8Encoding($false)
    $start.StandardErrorEncoding=New-Object Text.UTF8Encoding($false)
    $process=[Diagnostics.Process]::Start($start)
    try {
        $output=$process.StandardOutput.ReadToEndAsync()
        $errors=$process.StandardError.ReadToEndAsync()
        $bytes=[Text.Encoding]::UTF8.GetBytes(($request | ConvertTo-Json -Compress))
        $process.StandardInput.BaseStream.Write($bytes,0,$bytes.Length)
        $process.StandardInput.BaseStream.Flush()
        $process.StandardInput.Close()
        if(!$process.WaitForExit(15000)) {
            $process.Kill()
            throw 'Background provider timed out. Outcome is unknown; inspect before any further action and never retry automatically.'
        }
        if(!$output.Result){throw ('Background helper failed: '+$errors.Result)}
        Write-Output $output.Result.Trim()
        exit $process.ExitCode
    } finally {$process.Dispose()}
} catch {
    @{ok=$false;stop=$true;error=$_.Exception.Message} | ConvertTo-Json -Compress
    exit 1
}
