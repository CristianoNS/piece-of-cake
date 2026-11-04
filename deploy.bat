@echo off
rem Builds the app, closes the running copy, updates the installed files and starts it again.
rem Only changed files are copied, so a code-only change replaces just PieceOfCake.dll.
setlocal
cd /d "%~dp0"
set "TARGET=%LOCALAPPDATA%\Programs\Piece of Cake"

if not exist "%TARGET%\PieceOfCake.exe" (
  echo Installed app not found in "%TARGET%". Run the installer once first.
  exit /b 1
)

rem Build first: if it fails, the running app is left untouched.
call build.bat
if errorlevel 1 exit /b 1

taskkill /IM PieceOfCake.exe /F >nul 2>&1
timeout /t 1 /nobreak >nul

robocopy "publish" "%TARGET%" /E /NFL /NDL /NJH /NJS /NP
if errorlevel 8 (
  echo Copy failed.
  exit /b 1
)

start "" "%TARGET%\PieceOfCake.exe"
echo Done: updated and started.
exit /b 0
