@echo off
rem Builds installer-output\PieceOfCake-Setup.exe with Inno Setup 6.
rem Needs publish\PieceOfCake.exe. If it is missing, build.bat is run first (requires the .NET 8 SDK).
setlocal
cd /d "%~dp0.."

if not exist "publish\PieceOfCake.exe" call build.bat
if not exist "publish\PieceOfCake.exe" goto :noexe

set "ISCC=%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe"
if not exist "%ISCC%" set "ISCC=%ProgramFiles%\Inno Setup 6\ISCC.exe"
if not exist "%ISCC%" set "ISCC=%LocalAppData%\Programs\Inno Setup 6\ISCC.exe"
if not exist "%ISCC%" goto :noiscc

"%ISCC%" "installer\PieceOfCake.iss"
if errorlevel 1 goto :failed

echo Done: installer-output\PieceOfCake-Setup.exe
exit /b 0

:noexe
echo Missing publish\PieceOfCake.exe. Put the built file there or install the .NET 8 SDK and run build.bat.
exit /b 1

:noiscc
echo Inno Setup 6 was not found. Install it from https://jrsoftware.org/isdl.php and run this script again.
exit /b 1

:failed
echo Installer build failed.
exit /b 1
