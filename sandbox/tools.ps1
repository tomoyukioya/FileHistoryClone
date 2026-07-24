# Test helper for the Windows Sandbox session.
# Run inside the sandbox:
#   powershell -ep bypass -File C:\sandbox\tools.ps1 <command>
# Commands:
#   edit-sample   : append one line to Documents\sample.txt (one "edit & save")
#   edit-sample3  : edit sample.txt 3 times, 12 seconds apart (for retention test)
#   show-backup   : list all files under the backup Data folder
#   retention60   : set RetentionScanInterval=60 in appsettings.json (portable install)
#   retention60i  : same, but for the installed version (%LOCALAPPDATA%)
#   marker        : add "my-marker" to ExcludeDirs of the installed appsettings.json
#   show-config   : print appsettings.json (portable)
#   show-configi  : print appsettings.json (installed)
#   res640        : force display resolution to 640x480
#   res1280       : restore display resolution to 1280x800
param([Parameter(Position = 0)][string]$Command = "")

$Docs = Join-Path $env:USERPROFILE "Documents"
$PortableConfig = "C:\FileHistoryClone\appsettings.json"
$InstalledConfig = Join-Path $env:LOCALAPPDATA "Programs\FileHistoryClone\appsettings.json"
$BackupRoot = Join-Path $env:USERPROFILE "FileHistoryCloneBackup"

function Set-Retention60($path) {
    $raw = Get-Content $path -Raw
    $new = $raw -replace '"RetentionScanInterval":\s*\d+', '"RetentionScanInterval": 60'
    Set-Content $path $new -Encoding utf8
    Write-Host "RetentionScanInterval set to 60 in $path"
    Get-Content $path | Select-String RetentionScanInterval
}

function Set-Resolution($w, $h) {
    Add-Type @"
using System;
using System.Runtime.InteropServices;
public class Display {
  [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
  public struct DEVMODE {
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmDeviceName;
    public short dmSpecVersion, dmDriverVersion, dmSize, dmDriverExtra;
    public int dmFields, dmPositionX, dmPositionY, dmDisplayOrientation, dmDisplayFixedOutput;
    public short dmColor, dmDuplex, dmYResolution, dmTTOption, dmCollate;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmFormName;
    public short dmLogPixels;
    public int dmBitsPerPel, dmPelsWidth, dmPelsHeight, dmDisplayFlags, dmDisplayFrequency;
    public int dmICMMethod, dmICMIntent, dmMediaType, dmDitherType, dmReserved1, dmReserved2, dmPanningWidth, dmPanningHeight;
  }
  [DllImport("user32.dll")] public static extern int EnumDisplaySettings(string name, int modeNum, ref DEVMODE devMode);
  [DllImport("user32.dll")] public static extern int ChangeDisplaySettings(ref DEVMODE devMode, int flags);
}
"@
    $dm = New-Object Display+DEVMODE
    $dm.dmSize = [System.Runtime.InteropServices.Marshal]::SizeOf($dm)
    [Display]::EnumDisplaySettings($null, -1, [ref]$dm) | Out-Null
    $dm.dmPelsWidth = $w; $dm.dmPelsHeight = $h
    $dm.dmFields = 0x80000 -bor 0x100000   # DM_PELSWIDTH | DM_PELSHEIGHT
    $ret = [Display]::ChangeDisplaySettings([ref]$dm, 0)
    Write-Host "ChangeDisplaySettings($w x $h) returned $ret (0 = OK)"
}

switch ($Command) {
    "edit-sample" {
        Add-Content (Join-Path $Docs "sample.txt") "edited $(Get-Date -Format HH:mm:ss)"
        Write-Host "sample.txt edited."
        Get-Content (Join-Path $Docs "sample.txt")
    }
    "edit-sample3" {
        1..3 | ForEach-Object {
            Add-Content (Join-Path $Docs "sample.txt") "edit $_ $(Get-Date -Format HH:mm:ss)"
            Write-Host "edit $_ done"
            if ($_ -lt 3) { Start-Sleep 12 }
        }
    }
    "show-backup" {
        if (Test-Path $BackupRoot) {
            Get-ChildItem $BackupRoot -Recurse -File | Select-Object FullName, Length, LastWriteTime | Format-Table -AutoSize
        } else {
            Write-Host "No backup yet at $BackupRoot"
        }
    }
    "retention60"  { Set-Retention60 $PortableConfig }
    "retention60i" { Set-Retention60 $InstalledConfig }
    "marker" {
        $raw = Get-Content $InstalledConfig -Raw
        $new = $raw -replace '"ExcludeDirs":\s*\[', "`"ExcludeDirs`": [`r`n      `"my-marker`","
        Set-Content $InstalledConfig $new -Encoding utf8
        Write-Host "my-marker added to $InstalledConfig"
        Get-Content $InstalledConfig | Select-String my-marker
    }
    "show-config"  { Get-Content $PortableConfig }
    "show-configi" { Get-Content $InstalledConfig }
    "res640"  { Set-Resolution 640 480 }
    "res1280" { Set-Resolution 1280 800 }
    "ps" {
        # Get-Process is WMI-free (tasklist/taskkill hang when the WMI service is down)
        Get-Process | Where-Object { $_.ProcessName -match "FileHistory|Setup|tmp|taskkill|tasklist" } |
            Select-Object Id, ProcessName, StartTime | Format-Table -AutoSize
    }
    "kill-app" {
        try { Stop-Process -Name FileHistory -Force -ErrorAction Stop; Write-Host "FileHistory.exe stopped." }
        catch { Write-Host "FileHistory.exe not running." }
    }
    "unstick" {
        # The v1.0.0 installer waits forever on taskkill.exe when WMI is broken.
        # Killing the hung taskkill/tasklist lets the installer continue.
        foreach ($n in "taskkill", "tasklist") {
            $p = Get-Process -Name $n -ErrorAction SilentlyContinue
            if ($p) { $p | Stop-Process -Force; Write-Host "killed hung $n" }
            else { Write-Host "$n not running" }
        }
    }
    "wmi" {
        $svc = Get-Service winmgmt
        Write-Host "winmgmt status: $($svc.Status)"
        if ($svc.Status -ne "Running") {
            Write-Host "Trying to start winmgmt..."
            try { Start-Service winmgmt -ErrorAction Stop; Write-Host "started: $((Get-Service winmgmt).Status)" }
            catch { Write-Host "FAILED to start: $($_.Exception.Message)" }
        }
    }
    "kill-setup" {
        Get-Process | Where-Object { $_.ProcessName -like "FileHistoryCloneSetup*" -or $_.ProcessName -like "*.tmp" } |
            ForEach-Object { Write-Host "killing $($_.ProcessName) ($($_.Id))"; Stop-Process -Id $_.Id -Force }
    }
    "install100" {
        $local = Join-Path $env:TEMP "FileHistoryCloneSetup-1.0.0.exe"
        Copy-Item "C:\sandbox\FileHistoryCloneSetup-1.0.0.exe" $local -Force
        Write-Host "Copied to $local, launching..."
        Start-Process $local
    }
    "install101" {
        $local = Join-Path $env:TEMP "FileHistoryCloneSetup-1.0.1.exe"
        Copy-Item "C:\sandbox\FileHistoryCloneSetup-1.0.1.exe" $local -Force
        Write-Host "Copied to $local, launching..."
        Start-Process $local
    }
    default {
        Write-Host "Usage: powershell -ep bypass -File C:\sandbox\tools.ps1 <command>"
        Write-Host "Commands: edit-sample | edit-sample3 | show-backup | retention60 | retention60i | marker | show-config | show-configi | res640 | res1280 | ps | wmi | unstick | kill-app | kill-setup | install100 | install101"
    }
}
