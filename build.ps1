param(
    [string]$Configuration = "Release",
    [string]$Version = "1.0.0",
    [switch]$SkipInstaller
)

$ErrorActionPreference = "Stop"

$root = $PSScriptRoot
$dist = Join-Path $root "dist"
$publishDir = Join-Path $dist "publish"
$portableZip = Join-Path $dist "WindowsShutdownTimer-portable-win-x64.zip"
$setupExe = Join-Path $dist "WindowsShutdownTimer-Setup.exe"

Push-Location $root
try {
    Remove-Item $dist -Recurse -Force -ErrorAction SilentlyContinue
    New-Item -ItemType Directory -Path $dist | Out-Null

    dotnet restore .\WindowsShutdownTimer.sln
    dotnet build .\WindowsShutdownTimer.sln -c $Configuration
    dotnet run --project .\tests\WindowsShutdownTimer.Tests\WindowsShutdownTimer.Tests.csproj -c $Configuration
    dotnet publish .\src\WindowsShutdownTimer.App\WindowsShutdownTimer.App.csproj `
        -c $Configuration `
        -r win-x64 `
        --self-contained true `
        -o $publishDir `
        /p:PublishSingleFile=true `
        /p:Version=$Version

    Compress-Archive -Path (Join-Path $publishDir "*") -DestinationPath $portableZip -Force

    if (-not $SkipInstaller) {
        $iscc = Get-Command iscc.exe -ErrorAction SilentlyContinue
        if ($null -eq $iscc) {
            $defaultIscc = "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe"
            if (Test-Path $defaultIscc) {
                $iscc = @{ Source = $defaultIscc }
            }
        }

        if ($null -eq $iscc) {
            Write-Warning "Inno Setup 6 was not found. Portable zip was created, installer was skipped."
        }
        else {
            & $iscc.Source (Join-Path $root "installer\WindowsShutdownTimer.iss") "/DAppVersion=$Version"
            if (-not (Test-Path $setupExe)) {
                throw "Installer build finished but $setupExe was not found."
            }
        }
    }

    Write-Host ""
    Write-Host "Created:"
    Write-Host $portableZip
    if (Test-Path $setupExe) {
        Write-Host $setupExe
    }
}
finally {
    Pop-Location
}
