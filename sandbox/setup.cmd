@echo off
rem ============================================================
rem  FileHistoryClone sandbox test bootstrap
rem  (runs automatically at sandbox logon via the .wsb file)
rem  Comments are in English to avoid codepage issues in cmd.
rem ============================================================

rem The app reads/writes appsettings.json next to the exe,
rem so copy it out of the read-only mapped folder first.
xcopy /e /i /y C:\sandbox\publish C:\FileHistoryClone >nul

rem Create sample files in Documents (the default protected folder).
set DOCS=%USERPROFILE%\Documents
echo version 1 of sample.txt> "%DOCS%\sample.txt"
echo this file should NOT be backed up (*.tmp exclusion)> "%DOCS%\ignore-me.tmp"
mkdir "%DOCS%\SubFolder" 2>nul
echo nested file> "%DOCS%\SubFolder\nested.txt"

rem Open the test plan, then start the app.
rem (Windows Sandbox has no Notepad/WordPad, so use Edge - the only
rem  application bundled with the sandbox.)
set EDGE=C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe
if exist "%EDGE%" (
    start "" "%EDGE%" "file:///C:/sandbox/TESTPLAN.md"
) else (
    start "" msedge "file:///C:/sandbox/TESTPLAN.md"
)
start "" /d C:\FileHistoryClone C:\FileHistoryClone\FileHistory.exe
