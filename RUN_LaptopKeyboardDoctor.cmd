@echo off
setlocal
cd /d "%~dp0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Build.ps1" -Run
if errorlevel 1 (
  echo.
  echo KHONG THE KHOI DONG. Xem loi phia tren.
  pause
  exit /b 1
)
endlocal
