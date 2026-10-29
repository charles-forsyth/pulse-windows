# Runs a command line in the interactive session and waits for it to finish.
param([Parameter(Mandatory)][string]$Exe, [string]$Arguments = "", [int]$TimeoutSec = 60)
$user = [System.Security.Principal.WindowsIdentity]::GetCurrent().Name
$a = if ($Arguments) { New-ScheduledTaskAction -Execute $Exe -Argument $Arguments } else { New-ScheduledTaskAction -Execute $Exe }
$pr = New-ScheduledTaskPrincipal -UserId $user -LogonType Interactive -RunLevel Limited
Register-ScheduledTask -TaskName PulseTask -Action $a -Principal $pr -Force | Out-Null
Start-ScheduledTask -TaskName PulseTask
$t0 = Get-Date
do { Start-Sleep -Milliseconds 500 } while ((Get-ScheduledTask -TaskName PulseTask).State -eq 'Running' -and ((Get-Date) - $t0).TotalSeconds -lt $TimeoutSec)
Unregister-ScheduledTask -TaskName PulseTask -Confirm:$false
