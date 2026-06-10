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
    
    # Clean previous publish artifacts
    if (Test-Path $publishPath) {
        Remove-Item -Recurse -Force $publishPath
    }
    New-Item -ItemType Directory -Force -Path $publishPath | Out-Null
    
    $appSettingsPath = Join-Path $repoRoot "KicsitLibrary.Desktop/appsettings.json"
    $appSettings = Get-Content -Raw $appSettingsPath | ConvertFrom-Json
    $runtimeMode = if ($appSettings.SystemSettings.RuntimeStorageMode) { $appSettings.SystemSettings.RuntimeStorageMode } else { "Development" }
    $useReleaseDataRoot = if ($null -ne $appSettings.SystemSettings.UseReleaseDataRoot) { $appSettings.SystemSettings.UseReleaseDataRoot } else { $false }

    Write-Host "Runtime data mode: $runtimeMode"
    Write-Host "Use release data root: $useReleaseDataRoot"
    Write-Host "Publish output folder: $publishPath"
    Write-Host "This smoke script is non-destructive, does not launch the app, and does not intentionally modify any real user database."
    Write-Host "It does not test installer elevated permissions, shortcuts, uninstall behavior, or upgrade rollback."
    Write-Host ""

    Invoke-Step "Clean solution" {
        dotnet clean KicsitLibrary.slnx
    }

    Invoke-Step "Restore solution" {
        dotnet restore KicsitLibrary.slnx
    }

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

    Write-Host "==> Verifying publish folder content..."
    
    # 1. Verify folder exists
    if (-not (Test-Path $publishPath)) {
        throw "Publish folder does not exist at: $publishPath"
    }

    # 2. Verify main executable exists
    $exePath = Join-Path $publishPath "KicsitLibrary.Desktop.exe"
    if (-not (Test-Path $exePath)) {
        throw "Main executable not found in publish folder: $exePath"
    }
    Write-Host "  [PASS] Main executable exists: $exePath"

    # 3. Verify appsettings exists
    $configPath = Join-Path $publishPath "appsettings.json"
    if (-not (Test-Path $configPath)) {
        throw "Configuration file not found in publish folder: $configPath"
    }
    Write-Host "  [PASS] Configuration file exists: $configPath"

    # 4. Verify required DLL files exist
    $requiredDlls = @("KicsitLibrary.Core.dll", "KicsitLibrary.Data.dll", "KicsitLibrary.Services.dll", "KicsitLibrary.Reports.dll")
    foreach ($dll in $requiredDlls) {
        $dllPath = Join-Path $publishPath $dll
        if (-not (Test-Path $dllPath)) {
            throw "Required DLL not found in publish folder: $dll"
        }
    }
    Write-Host "  [PASS] All required KicsitLibrary DLLs exist in publish folder."

    # 5. Verify no development database is accidentally copied
    $dbPath = Join-Path $publishPath "KicsitLibrary.db"
    if (Test-Path $dbPath) {
        throw "Stray development database KicsitLibrary.db was accidentally copied to publish folder!"
    }
    Write-Host "  [PASS] No stray development database found in publish folder."

    Write-Host ""
    Write-Host "Deployment smoke test completed successfully."
    Write-Host "Publish output: $publishPath"
    Write-Host "The application was not launched, no real user database was intentionally modified, and no production installer was created."
}
catch {
    Write-Host ""
    Write-Host "Deployment smoke test failed: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
