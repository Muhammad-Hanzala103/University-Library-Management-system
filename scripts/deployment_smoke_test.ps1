param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$PublishDirectory = "artifacts/deployment-smoke/publish"
)

$ErrorActionPreference = "Stop"

function Invoke-Step {
    param(
        [string]$Name,
        [scriptblock]$Command
    )

    Write-Host "==> $Name"
    & $Command
    if ($LASTEXITCODE -ne 0) {
        throw "$Name failed with exit code $LASTEXITCODE."
    }
}

try {
    $repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
    Set-Location $repoRoot

    $publishPath = Join-Path $repoRoot $PublishDirectory
    New-Item -ItemType Directory -Force -Path $publishPath | Out-Null

    Invoke-Step "Build solution" {
        dotnet build KicsitLibrary.slnx
    }

    Invoke-Step "Run automated tests" {
        dotnet test KicsitLibrary.slnx
    }

    Invoke-Step "Publish desktop project to local smoke folder" {
        dotnet publish KicsitLibrary.Desktop/KicsitLibrary.Desktop.csproj `
            -c $Configuration `
            -r $Runtime `
            --self-contained false `
            -o $publishPath
    }

    Write-Host ""
    Write-Host "Deployment smoke test completed successfully."
    Write-Host "Publish output: $publishPath"
    Write-Host "The application was not launched and no production installer was created."
}
catch {
    Write-Host ""
    Write-Host "Deployment smoke test failed: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
