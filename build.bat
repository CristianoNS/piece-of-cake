@echo off
rem Builds a self-contained PieceOfCake into the "publish" folder as a normal set of files
rem (PieceOfCake.exe + PieceOfCake.dll + runtime libraries), so single files can be replaced later.
rem Requires the .NET 8 SDK: https://dotnet.microsoft.com/download/dotnet/8.0
setlocal
cd /d "%~dp0"
if exist publish rmdir /s /q publish
dotnet publish src\PieceOfCake\PieceOfCake.csproj -c Release -r win-x64 --self-contained true -p:DebugType=none -o publish
if errorlevel 1 (
  echo Build failed.
  exit /b 1
)
echo Done: publish\PieceOfCake.exe
