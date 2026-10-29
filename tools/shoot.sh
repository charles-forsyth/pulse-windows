#!/usr/bin/env bash
# usage: tools/shoot.sh <exe> <page> <outname> [wait]
# Launches Pulse in the interactive session on a given page and screenshots its window
# to %LOCALAPPDATA%\Pulse\shots\<outname>.png
EXE="$1"; PAGE="$2"; OUT="$3"; WAIT="${4:-9}"
cd "$(dirname "$0")/.."
pwsh -NoProfile -Command 'Get-Process Pulse -ErrorAction SilentlyContinue | Stop-Process -Force' >/dev/null 2>&1
pwsh -NoProfile -ExecutionPolicy Bypass -Command "& ./tools/run-task.ps1 -Exe '$EXE' -Arguments '--page $PAGE' -TimeoutSec 3" >/dev/null 2>&1
sleep "$WAIT"
SHOT="$env:LOCALAPPDATA\\Pulse\\shots\\$OUT.png"
pwsh -NoProfile -ExecutionPolicy Bypass -Command "& ./tools/run-task.ps1 -Exe powershell.exe -Arguments \"-NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -File (Join-Path `$PSScriptRoot screenshot.ps1) -Out `\"$SHOT`\"\""
