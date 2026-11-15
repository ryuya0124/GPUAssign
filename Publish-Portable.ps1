$ErrorActionPreference = 'Stop'
foreach ($taskMode in @('SelfContained', 'FrameworkDependent')) {
    $taskOutput = Join-Path $PSScriptRoot "publish/GPUAssign-$taskMode"
    $taskStandalone = ($taskMode -eq 'SelfContained').ToString().ToLowerInvariant()
    & dotnet publish (Join-Path $PSScriptRoot 'GPUAssign.csproj') -c Release -p:Platform=x64 -r win-x64 --self-contained $taskStandalone -o $taskOutput
    if ($LASTEXITCODE -ne 0) { throw "Publish failed: $LASTEXITCODE" }
    Write-Host "Portable app: $taskOutput\GPUAssign.exe"
}
