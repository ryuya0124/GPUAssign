@echo off
set MSBUILD="C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe"

echo ======================================================================
echo [1/2] Building Self-Contained Portable Package (同梱版)...
echo ======================================================================
%MSBUILD% GPUAssign.csproj /p:Configuration=Release /p:Platform=x64 /p:RuntimeIdentifier=win-x64 /p:SelfContained=true /p:PublishDir="publish\GPUAssign-SelfContained\\" /t:Publish /m /nologo

echo ======================================================================
echo [2/2] Building Framework-Dependent Portable Package (非同梱版)...
echo ======================================================================
%MSBUILD% GPUAssign.csproj /p:Configuration=Release /p:Platform=x64 /p:RuntimeIdentifier=win-x64 /p:SelfContained=false /p:PublishDir="publish\GPUAssign-FrameworkDependent\\" /t:Publish /m /nologo

echo ======================================================================
echo Build Complete!
echo - Self-Contained:       publish\GPUAssign-SelfContained\
echo - Framework-Dependent:  publish\GPUAssign-FrameworkDependent\
echo ======================================================================
