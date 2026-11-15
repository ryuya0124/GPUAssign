@echo off
setlocal
set CONFIG=Debug
if /i "%~1"=="release" set CONFIG=Release
dotnet build "%~dp0GPUAssign.csproj" -c %CONFIG% -p:Platform=x64 -r win-x64
exit /b %ERRORLEVEL%
