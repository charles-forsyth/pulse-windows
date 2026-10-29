# Publishes Pulse as a self-contained single-file exe for win-arm64 and win-x64,
# copies to Desktop\Pulse\<arch>\ and zips each to Desktop\Pulse-<arch>.zip.
param([string[]]$Rids = @('win-arm64', 'win-x64'))
$ErrorActionPreference = 'Stop'
$proj = Join-Path $PSScriptRoot '..\Pulse.csproj'
$desk = [Environment]::GetFolderPath('Desktop')
$root = Join-Path $desk 'Pulse'
$logs = Join-Path $env:LOCALAPPDATA 'Pulse\build-logs'
New-Item -ItemType Directory -Force -Path $root, $logs | Out-Null

$results = @()
foreach ($rid in $Rids) {
    $arch = $rid.Split('-')[1]
    $out = Join-Path $root $arch
    if (Test-Path $out) { Remove-Item -Recurse -Force $out }
    $log = Join-Path $logs "publish-$rid.log"
    Write-Host "==> dotnet publish $rid"
    & dotnet publish $proj -c Release -r $rid --self-contained true `
        -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:WindowsAppSDKSelfContained=true -p:PublishReadyToRun=true `
        -p:DebugType=none -p:DebugSymbols=false -p:EnableCompressionInSingleFile=true `
        -o $out 2>&1 | Tee-Object -FilePath $log | Out-Null
    $code = $LASTEXITCODE
    $warn = @(Select-String -Path $log -Pattern ' warning ' | ForEach-Object { ($_.Line -replace '\s*\[.*$', '') } | Sort-Object -Unique)
    $err = @(Select-String -Path $log -Pattern ' error ' | ForEach-Object { ($_.Line -replace '\s*\[.*$', '') } | Sort-Object -Unique)
    Write-Host "    exit=$code warnings=$($warn.Count) errors=$($err.Count)"
    $warn | ForEach-Object { Write-Host "    WARN $_" }
    $err | ForEach-Object { Write-Host "    ERR  $_" }
    if ($code -ne 0) { throw "publish $rid failed ($code)" }

    $zip = Join-Path $desk "Pulse-$arch.zip"
    if (Test-Path $zip) { Remove-Item -Force $zip }
    Compress-Archive -Path (Join-Path $out '*') -DestinationPath $zip -CompressionLevel Optimal
    $results += [pscustomobject]@{ Rid = $rid; Exit = $code; Warnings = $warn.Count; Files = (Get-ChildItem $out).Count; Exe = (Join-Path $out 'Pulse.exe'); ExeMB = [math]::Round((Get-Item (Join-Path $out 'Pulse.exe')).Length / 1MB, 1); Zip = $zip; ZipMB = [math]::Round((Get-Item $zip).Length / 1MB, 1) }
}
$results | Format-List
