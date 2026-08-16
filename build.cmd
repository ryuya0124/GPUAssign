@echo off
REM Build script using Visual Studio MSBuild (avoids dotnet CLI XAML compiler culture issue)
set MSBUILD="C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe"

if "%1"=="release" (
    %MSBUILD% GPUAssign.csproj /p:Configuration=Release /p:Platform=x64 /p:RuntimeIdentifier=win-x64 /m /nologo
) else (
    %MSBUILD% GPUAssign.csproj /p:Configuration=Debug /p:Platform=x64 /p:RuntimeIdentifier=win-x64 /m /nologo
)
