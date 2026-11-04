@echo off
rem Builds the app, closes the running copy, updates the installed files and starts it again.
rem The build is a single PieceOfCake.exe, so deploying replaces that one file.
setlocal
cd /d "%~dp0"
set "TARGET=%LOCALAPPDATA%\Programs\Piece of Cake"

if not exist "%TARGET%\PieceOfCake.exe" (
  echo Installed app not found in "%TARGET%". Run the installer once first.
  exit /b 1
)

rem Build first: if it fails, the running app is left untouched.
call "%~dp0build.bat"
if errorlevel 1 exit /b 1

taskkill /IM PieceOfCake.exe /F >nul 2>&1
rem ping instead of timeout: timeout fails when stdin is redirected (scripts, CI, Claude).
ping -n 2 127.0.0.1 >nul

robocopy "publish" "%TARGET%" /E /NFL /NDL /NJH /NJS /NP
if errorlevel 8 (
  echo Copy failed.
  exit /b 1
)

rem Redirect output so a caller capturing it does not wait for the app to exit.
start "" "%TARGET%\PieceOfCake.exe" >nul 2>&1
echo Done: updated and started.
exit /b 0
