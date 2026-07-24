# Host-driven script runner: executes C:\shots\cmd.ps1 (written by the host via the
# writable share) and captures all output to C:\shots\out.txt.
$out = "C:\shots\out.txt"
"" | Out-File $out -Encoding utf8
try {
    & "C:\shots\cmd.ps1" *>&1 | Out-File $out -Encoding utf8
} catch {
    $_ | Out-File $out -Encoding utf8 -Append
}
