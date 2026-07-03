param(
    [string]$Configuration = "Release",
    [string]$Version = "1.0.0"
)

$ErrorActionPreference = "Stop"

$root = $PSScriptRoot
$dist = Join-Path $root "dist"
$publishDir = Join-Path $dist "publish"
$setupExe = Join-Path $dist "WindowsShutdownTimer-Setup.exe"

function Invoke-NativeCommand {
    param(
        [Parameter(Mandatory = $true)]
        [scriptblock]$Command,
        [Parameter(Mandatory = $true)]
        [string]$Name
    )

    & $Command
    if ($LASTEXITCODE -ne 0) {
        throw "$Name failed with exit code $LASTEXITCODE."
    }
}

Push-Location $root
try {
    Remove-Item $dist -Recurse -Force -ErrorAction SilentlyContinue
    New-Item -ItemType Directory -Path $dist | Out-Null

    Invoke-NativeCommand -Name "dotnet restore" -Command { dotnet restore .\WindowsShutdownTimer.sln }
    Invoke-NativeCommand -Name "dotnet build" -Command { dotnet build .\WindowsShutdownTimer.sln -c $Configuration }
    Invoke-NativeCommand -Name "tests" -Command { dotnet run --project .\tests\WindowsShutdownTimer.Tests\WindowsShutdownTimer.Tests.csproj -c $Configuration }
    Invoke-NativeCommand -Name "dotnet publish" -Command {
        dotnet publish .\src\WindowsShutdownTimer.App\WindowsShutdownTimer.App.csproj `
            -c $Configuration `
            -r win-x64 `
            --self-contained true `
            -o $publishDir `
            /p:PublishSingleFile=true `
            /p:Version=$Version
    }

    $iscc = Get-Command iscc.exe -ErrorAction SilentlyContinue
    if ($null -eq $iscc) {
        $defaultIscc = "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe"
        if (Test-Path $defaultIscc) {
            $iscc = @{ Source = $defaultIscc }
        }
    }

    if ($null -eq $iscc) {
        throw "Inno Setup 6 was not found. Install it to create WindowsShutdownTimer-Setup.exe."
    }

    Invoke-NativeCommand -Name "Inno Setup" -Command {
        & $iscc.Source (Join-Path $root "installer\WindowsShutdownTimer.iss") "/DAppVersion=$Version"
    }
    if (-not (Test-Path $setupExe)) {
        throw "Installer build finished but $setupExe was not found."
    }

    Write-Host ""
    Write-Host "Created:"
    Write-Host $setupExe
}
finally {
    Pop-Location
}
