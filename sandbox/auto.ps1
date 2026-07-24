# In-sandbox automation helpers driven from the host via `wsb exec`.
# Results (screenshots, command output) go to C:\shots (writable share).
# Usage examples:
#   powershell -ep bypass -File C:\sandbox\auto.ps1 shot
#   powershell -ep bypass -File C:\sandbox\auto.ps1 click 100 200
#   powershell -ep bypass -File C:\sandbox\auto.ps1 rclick 100 200
#   powershell -ep bypass -File C:\sandbox\auto.ps1 dclick 100 200
#   powershell -ep bypass -File C:\sandbox\auto.ps1 keys "^s"        (SendKeys format)
#   powershell -ep bypass -File C:\sandbox\auto.ps1 move 100 200
param(
    [Parameter(Position = 0)][string]$Command = "",
    [Parameter(Position = 1)][string]$Arg1 = "",
    [Parameter(Position = 2)][string]$Arg2 = "",
    [Parameter(Position = 3)][string]$Arg3 = ""
)

$OutDir = "C:\shots"

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
Add-Type @"
using System;
using System.Runtime.InteropServices;
public class Auto {
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern void mouse_event(uint flags, uint dx, uint dy, uint data, UIntPtr extra);
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
  [DllImport("user32.dll")] public static extern int GetSystemMetrics(int i);
  public const uint LEFTDOWN = 0x02, LEFTUP = 0x04, RIGHTDOWN = 0x08, RIGHTUP = 0x10;
}
"@
# Physical-pixel coordinates everywhere (screenshots and clicks agree).
[Auto]::SetProcessDPIAware() | Out-Null

function Take-Shot {
    $w = [Auto]::GetSystemMetrics(78)   # SM_CXVIRTUALSCREEN
    $h = [Auto]::GetSystemMetrics(79)   # SM_CYVIRTUALSCREEN
    $bmp = New-Object System.Drawing.Bitmap $w, $h
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.CopyFromScreen([Auto]::GetSystemMetrics(76), [Auto]::GetSystemMetrics(77), 0, 0, $bmp.Size)
    $g.Dispose()
    $bmp.Save((Join-Path $OutDir "screen.png"), [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
}

function Click([int]$x, [int]$y, [string]$btn, [bool]$double) {
    [Auto]::SetCursorPos($x, $y) | Out-Null
    Start-Sleep -Milliseconds 150
    if ($btn -eq "right") {
        [Auto]::mouse_event([Auto]::RIGHTDOWN, 0, 0, 0, [UIntPtr]::Zero)
        [Auto]::mouse_event([Auto]::RIGHTUP, 0, 0, 0, [UIntPtr]::Zero)
    } else {
        [Auto]::mouse_event([Auto]::LEFTDOWN, 0, 0, 0, [UIntPtr]::Zero)
        [Auto]::mouse_event([Auto]::LEFTUP, 0, 0, 0, [UIntPtr]::Zero)
        if ($double) {
            Start-Sleep -Milliseconds 80
            [Auto]::mouse_event([Auto]::LEFTDOWN, 0, 0, 0, [UIntPtr]::Zero)
            [Auto]::mouse_event([Auto]::LEFTUP, 0, 0, 0, [UIntPtr]::Zero)
        }
    }
}

switch ($Command) {
    "shot"   { Take-Shot }
    "click"  { Click ([int]$Arg1) ([int]$Arg2) "left" $false; Start-Sleep -Milliseconds 400; Take-Shot }
    "rclick" { Click ([int]$Arg1) ([int]$Arg2) "right" $false; Start-Sleep -Milliseconds 400; Take-Shot }
    "dclick" { Click ([int]$Arg1) ([int]$Arg2) "left" $true; Start-Sleep -Milliseconds 400; Take-Shot }
    "move"   { [Auto]::SetCursorPos([int]$Arg1, [int]$Arg2) | Out-Null; Start-Sleep -Milliseconds 600; Take-Shot }
    "keys"   { [System.Windows.Forms.SendKeys]::SendWait($Arg1); Start-Sleep -Milliseconds 400; Take-Shot }
    "seq"    {
        # Execute a sequence from C:\shots\seq.txt (one action per line) in ONE process,
        # so transient UI (tray flyouts, menus) stays open between steps.
        foreach ($line in Get-Content (Join-Path $OutDir "seq.txt")) {
            $t = $line.Trim(); if ($t -eq "") { continue }
            $a = $t -split '\s+', 3
            switch ($a[0]) {
                "click"  { Click ([int]$a[1]) ([int]$a[2]) "left" $false }
                "rclick" { Click ([int]$a[1]) ([int]$a[2]) "right" $false }
                "dclick" { Click ([int]$a[1]) ([int]$a[2]) "left" $true }
                "move"   { [Auto]::SetCursorPos([int]$a[1], [int]$a[2]) | Out-Null }
                "sleep"  { Start-Sleep -Milliseconds ([int]$a[1]) }
                "keys"   { [System.Windows.Forms.SendKeys]::SendWait($a[1]) }
                "type"   { [System.Windows.Forms.SendKeys]::SendWait(($a[1..($a.Count-1)] -join " ")) }
                "shot"   { Take-Shot; $script:shotIdx++; Copy-Item (Join-Path $OutDir "screen.png") (Join-Path $OutDir "screen$($script:shotIdx).png") -Force }
            }
        }
        Take-Shot
    }
    default  { "Usage: auto.ps1 shot|click x y|rclick x y|dclick x y|move x y|keys <sendkeys>" | Out-File (Join-Path $OutDir "out.txt") }
}
