# Captures the primary screen (or the Pulse window if found) to a PNG. Must run in the interactive session.
param([string]$Out = "$env:LOCALAPPDATA\Pulse\shot.png")
Add-Type -AssemblyName System.Drawing, System.Windows.Forms
Add-Type @"
using System; using System.Runtime.InteropServices;
public static class W {
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  public struct RECT { public int L, T, R, B; }
}
"@
[W]::SetProcessDPIAware() | Out-Null
$p = Get-Process Pulse -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
if ($p) {
  [W]::SetForegroundWindow($p.MainWindowHandle) | Out-Null; Start-Sleep -Milliseconds 600
  $r = New-Object W+RECT; [W]::GetWindowRect($p.MainWindowHandle, [ref]$r) | Out-Null
  $x=$r.L; $y=$r.T; $w=$r.R-$r.L; $h=$r.B-$r.T
} else { $b=[System.Windows.Forms.Screen]::PrimaryScreen.Bounds; $x=$b.X;$y=$b.Y;$w=$b.Width;$h=$b.Height }
$bmp = New-Object System.Drawing.Bitmap $w, $h
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.CopyFromScreen($x, $y, 0, 0, $bmp.Size)
New-Item -ItemType Directory -Force -Path (Split-Path $Out) | Out-Null
$bmp.Save($Out, [System.Drawing.Imaging.ImageFormat]::Png)
"title=$($p.MainWindowTitle) rect=$x,$y,$w,$h" | Out-File "$Out.txt"
