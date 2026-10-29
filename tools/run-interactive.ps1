# Launches an exe in the logged-on user's interactive desktop session (needed when the caller is in Session 0,
# e.g. SSH/WinRM). Uses a one-shot scheduled task with an Interactive logon token.
param([Parameter(Mandatory)][string]$Exe, [string]$TaskName = "PulseLaunch", [string]$Env = "")
$ErrorActionPreference = 'Stop'
$user = [System.Security.Principal.WindowsIdentity]::GetCurrent().Name
$cmd = if ($Env) { "cmd.exe" } else { $Exe }
$arg = if ($Env) { "/c set `"$Env`"&& start `"`" `"$Exe`"" } else { "" }
$action = if ($arg) { New-ScheduledTaskAction -Execute $cmd -Argument $arg -WorkingDirectory (Split-Path $Exe) } else { New-ScheduledTaskAction -Execute $cmd -WorkingDirectory (Split-Path $Exe) }
$principal = New-ScheduledTaskPrincipal -UserId $user -LogonType Interactive -RunLevel Limited
$settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -ExecutionTimeLimit ([TimeSpan]::Zero)
Register-ScheduledTask -TaskName $TaskName -Action $action -Principal $principal -Settings $settings -Force | Out-Null
Start-ScheduledTask -TaskName $TaskName
Start-Sleep -Seconds 2
Unregister-ScheduledTask -TaskName $TaskName -Confirm:$false
