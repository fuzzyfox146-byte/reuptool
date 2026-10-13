@echo off
cd /d "%~dp0"
if exist "%~dp0app\YtFolderUploader.exe" (
  start "" "%~dp0app\YtFolderUploader.exe"
) else (
  dotnet run --project "%~dp0YtFolderUploader\YtFolderUploader.csproj"
  if errorlevel 1 pause
)
