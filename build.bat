@echo off
rem Builds a self-contained, single-file PieceOfCake.exe (runtime included, no .NET install needed)
rem into the "publish" folder. Not compressed on purpose: compression decompresses all assemblies into
rem private memory at startup (measured: about 76 MB more RAM).
rem Requires the .NET 10 SDK: https://dotnet.microsoft.com/download/dotnet/10.0
setlocal
cd /d "%~dp0"
if exist publish rmdir /s /q publish
dotnet publish src\PieceOfCake\PieceOfCake.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=none -o publish
if errorlevel 1 (
  echo Build failed.
  exit /b 1
)
echo Done: publish\PieceOfCake.exe
