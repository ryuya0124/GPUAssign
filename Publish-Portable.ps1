$msbuild = "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe"

Write-Host "======================================================================" -ForegroundColor Cyan
Write-Host "  GPU Assign - Portable Package Publisher" -ForegroundColor Cyan
Write-Host "======================================================================" -ForegroundColor Cyan
Write-Host ""

# 1. Self-Contained (All dependencies included)
Write-Host "[1/2] Building Self-Contained Portable Package (同梱版)..." -ForegroundColor Yellow
$out1 = "publish\GPUAssign-SelfContained\"
New-Item -ItemType Directory -Force -Path $out1 | Out-Null
& $msbuild GPUAssign.csproj /p:Configuration=Release /p:Platform=x64 /p:RuntimeIdentifier=win-x64 /p:SelfContained=true /p:PublishDir="$PWD\$out1" /t:Publish /m /nologo

if ($LASTEXITCODE -ne 0) {
    Write-Host "[ERROR] Self-Contained build failed." -ForegroundColor Red
    exit $LASTEXITCODE
}
Write-Host "[OK] Self-Contained package ready at: $out1" -ForegroundColor Green
Write-Host ""

# 2. Framework-Dependent (Lightweight)
Write-Host "[2/2] Building Framework-Dependent Portable Package (非同梱版)..." -ForegroundColor Yellow
$out2 = "publish\GPUAssign-FrameworkDependent\"
New-Item -ItemType Directory -Force -Path $out2 | Out-Null
& $msbuild GPUAssign.csproj /p:Configuration=Release /p:Platform=x64 /p:RuntimeIdentifier=win-x64 /p:SelfContained=false /p:PublishDir="$PWD\$out2" /t:Publish /m /nologo

if ($LASTEXITCODE -ne 0) {
    Write-Host "[ERROR] Framework-Dependent build failed." -ForegroundColor Red
    exit $LASTEXITCODE
}
Write-Host "[OK] Framework-Dependent package ready at: $out2" -ForegroundColor Green
Write-Host ""

Write-Host "======================================================================" -ForegroundColor Cyan
Write-Host "  Build Completed Successfully!" -ForegroundColor Cyan
Write-Host "  - 同梱版 (Self-Contained):      $PWD\$out1" -ForegroundColor Green
Write-Host "  - 非同梱版 (Framework-Dependent): $PWD\$out2" -ForegroundColor Green
Write-Host "======================================================================" -ForegroundColor Cyan
