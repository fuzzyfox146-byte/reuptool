@echo off
chcp 65001 >nul
cd /d "%~dp0"
set "PY=%LocalAppData%\Programs\Python\Python312\python.exe"
if not exist "%PY%" set "PY=py"
"%PY%" ui.py
