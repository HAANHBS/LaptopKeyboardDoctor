@echo off
setlocal
cd /d "%~dp0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Build.ps1"
if errorlevel 1 (
  echo.
  echo BUILD/SELF-TEST: FAIL
  pause
  exit /b 1
)
echo.
echo BUILD/SELF-TEST: PASS
pause
endlocal
