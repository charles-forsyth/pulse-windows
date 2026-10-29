# Verifies a published Pulse.exe: launch in the interactive session, confirm it stays alive >= $Seconds,
# check the Application event log for .NET Runtime / Application Error / WER entries, then stop it.
param([Parameter(Mandatory)][string]$Exe, [int]$Seconds = 20)
$ErrorActionPreference = 'Stop'
Get-Process Pulse -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep 1
$start = Get-Date
& (Join-Path $PSScriptRoot 'run-task.ps1') -Exe $Exe -TimeoutSec 3
$p = $null
for ($i = 0; $i -lt 20 -and -not $p; $i++) { Start-Sleep -Milliseconds 500; $p = Get-Process Pulse -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $Exe } | Select-Object -First 1 }
if (-not $p) { "RESULT: process never appeared"; }
else {
    "Launched PID $($p.Id) in session $($p.SessionId) at $($p.StartTime.ToString('HH:mm:ss'))"
    $samples = @()
    for ($t = 5; $t -le $Seconds; $t += 5) {
        Start-Sleep 5
        $q = Get-Process -Id $p.Id -ErrorAction SilentlyContinue
        if (-not $q) { "  t+${t}s: PROCESS EXITED"; break }
        $samples += $q
        "  t+${t}s: alive, Responding=$($q.Responding), WS=$([math]::Round($q.WorkingSet64/1MB)) MB, CPU=$([math]::Round($q.CPU,1)) s, Threads=$($q.Threads.Count), Window='$($q.MainWindowTitle)'"
    }
    $alive = [bool](Get-Process -Id $p.Id -ErrorAction SilentlyContinue)
    $elapsed = [math]::Round(((Get-Date) - $p.StartTime).TotalSeconds, 1)
    "Alive after ${elapsed}s: $alive"
    $events = @(Get-WinEvent -FilterHashtable @{ LogName = 'Application'; StartTime = $start } -ErrorAction SilentlyContinue |
        Where-Object { $_.ProviderName -in @('.NET Runtime', 'Application Error', 'Windows Error Reporting', 'Application Hang') -and $_.Message -match 'Pulse' })
    "Application log entries (.NET Runtime / Application Error / WER / Hang) mentioning Pulse since launch: $($events.Count)"
    $events | ForEach-Object { "  [$($_.TimeCreated.ToString('HH:mm:ss'))] $($_.ProviderName) $($_.Id): $(($_.Message -split "`n")[0..3] -join ' | ')" }
    $all = @(Get-WinEvent -FilterHashtable @{ LogName = 'Application'; StartTime = $start; Level = 1, 2 } -ErrorAction SilentlyContinue)
    "All Error/Critical Application log entries since launch (any source): $($all.Count)"
    $all | Select-Object -First 5 | ForEach-Object { "  [$($_.TimeCreated.ToString('HH:mm:ss'))] $($_.ProviderName) $($_.Id): $(($_.Message -split "`n")[0])" }
    if ($alive) { Stop-Process -Id $p.Id; Start-Sleep 1; "Stopped PID $($p.Id): $(-not [bool](Get-Process -Id $p.Id -ErrorAction SilentlyContinue))" }
}
