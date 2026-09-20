@echo off
setlocal
cd /d "%~dp0"

if "%~1"=="" (
  powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0trim-background-start.ps1"
) else (
  powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0trim-background-start.ps1" -Folder "%~1" %2 %3 %4 %5
)

echo.
pause
endlocal
