<#
.SYNOPSIS
    Ilm o Kutub System — Release Security Scan Script
.DESCRIPTION
    Scans the repository for sensitive patterns, default passwords,
    private paths, and risky content before a GitHub push or release.
.NOTES
    Exit code 0 = PASS (no high-risk findings)
    Exit code 1 = FAIL (high-risk patterns detected)
#>

param(
    [switch]$Verbose
)

$ErrorActionPreference = 'Continue'
$script:exitCode = 0
$script:warnings = 0
$script:passes = 0
$repoRoot = Split-Path -Parent $PSScriptRoot
if (-not (Test-Path (Join-Path $repoRoot 'KicsitLibrary.slnx'))) { $repoRoot = (Get-Location).Path }

function Write-Pass($message) {
    Write-Host "  [PASS] $message" -ForegroundColor Green
    $script:passes++
}

function Write-Warn($message) {
    Write-Host "  [WARN] $message" -ForegroundColor Yellow
    $script:warnings++
}

function Write-Fail($message) {
    Write-Host "  [FAIL] $message" -ForegroundColor Red
    $script:exitCode = 1
}

function Write-Info($message) {
    if ($Verbose) { Write-Host "  [INFO] $message" -ForegroundColor Cyan }
}

Write-Host ""
Write-Host "=============================================" -ForegroundColor Cyan
Write-Host "  Ilm o Kutub System — Security Scan" -ForegroundColor Cyan
Write-Host "=============================================" -ForegroundColor Cyan
Write-Host ""

# ─── 1. Default Password Exposure in Public Docs ───
Write-Host "[1/8] Checking public documentation for exposed passwords..." -ForegroundColor White

$publicDocs = @(
    "README.md",
    "RELEASE NOTES.md",
    "INSTALLATION GUIDE.md",
    "DEMO CHECKLIST.md",
    "SCREENSHOTS GUIDE.md"
)

$passwordPatterns = @(
    "SuperAdmin123",
    "Admin123!",
    "Librarian123",
    "Assistant123",
    "Auditor123",
    "Viewer123"
)

$docPasswordFound = $false
foreach ($doc in $publicDocs) {
    $docPath = Join-Path $repoRoot $doc
    if (Test-Path $docPath) {
        $content = Get-Content $docPath -Raw -ErrorAction SilentlyContinue
        foreach ($pattern in $passwordPatterns) {
            if ($content -match [regex]::Escape($pattern)) {
                Write-Fail "$doc contains exposed password pattern: $pattern"
                $docPasswordFound = $true
            }
        }
    }
}
if (-not $docPasswordFound) {
    Write-Pass "No default passwords found in public documentation."
}

# ─── 2. SMTP Password in Non-Service Files ───
Write-Host "[2/8] Checking for SMTP password exposure..." -ForegroundColor White

$smtpSafeFiles = @(
    "DbSeeder.cs",
    "SettingsManagementService.cs",
    "EmailSettingsService.cs",
    "SqliteTestDatabase.cs",
    "SettingsManagementServiceTests.cs",
    "DatabaseFoundationTests.cs",
    "DatabaseOwnershipServiceTests.cs",
    "BackupWorkflowTests.cs",
    "RestoreWorkflowTests.cs",
    "EmailDeliveryTests.cs",
    "SecurityHardeningTests.cs",
    "security_scan.ps1",
    "KNOWN ISSUES.md",
    "TEST COMMANDS.md",
    "CURRENT STATUS.md",
    "CODEX CONTINUATION AUDIT.md",
    "DEPLOYMENT READINESS AUDIT.md",
    "PACKAGING STRATEGY.md",
    "RELEASE NOTES.md",
    "RELEASE SECURITY NOTES.md",
    "SECURITY CHECKLIST.md"
)

$smtpIssueFound = $false
$allFiles = Get-ChildItem -Path $repoRoot -Recurse -Include "*.cs","*.md","*.json","*.ps1","*.xaml" -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -notmatch '\\(bin|obj|\.vs|\.git|artifacts|TestResults)\\' }

foreach ($file in $allFiles) {
    $content = Get-Content $file.FullName -Raw -ErrorAction SilentlyContinue
    if ($content -match 'SmtpPassword.*=.*"[^"]{4,}"') {
        $baseName = $file.Name
        if ($baseName -notin $smtpSafeFiles) {
            Write-Fail "$($file.Name) may contain an SMTP password value assignment."
            $smtpIssueFound = $true
        }
    }
}
if (-not $smtpIssueFound) {
    Write-Pass "No unexpected SMTP password values found."
}

# ─── 3. Private Local Paths ───
Write-Host "[3/8] Checking for private local machine paths..." -ForegroundColor White

$pathPatterns = @(
    'C:\\Users\\[a-zA-Z]',
    'D:\\Users\\[a-zA-Z]',
    '/home/[a-zA-Z]'
)

$pathIssueFound = $false
$mdFiles = Get-ChildItem -Path $repoRoot -Filter "*.md" -ErrorAction SilentlyContinue |
    Where-Object { $_.Name -ne "DEMO CREDENTIALS PRIVATE TEMPLATE.md" }

foreach ($file in $mdFiles) {
    $content = Get-Content $file.FullName -Raw -ErrorAction SilentlyContinue
    foreach ($pattern in $pathPatterns) {
        if ($content -match $pattern) {
            # Allow generic %LOCALAPPDATA% and %USERPROFILE% references
            $lines = Get-Content $file.FullName -ErrorAction SilentlyContinue
            foreach ($line in $lines) {
                if ($line -match $pattern -and $line -notmatch '%[A-Z]+%' -and $line -notmatch '<.*>') {
                    Write-Warn "$($file.Name) may contain a private local path: $($line.Trim().Substring(0, [Math]::Min(80, $line.Trim().Length)))"
                    $pathIssueFound = $true
                }
            }
        }
    }
}
if (-not $pathIssueFound) {
    Write-Pass "No private local machine paths found in documentation."
}

# ─── 4. Database Files in Repository ───
Write-Host "[4/8] Checking for database files in repository..." -ForegroundColor White

$dbFiles = Get-ChildItem -Path $repoRoot -Recurse -Include "*.db","*.db-wal","*.db-shm","*.sqlite" -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -notmatch '\\(bin|obj|\.vs|artifacts|TestResults|publish)\\' }

if ($dbFiles.Count -gt 0) {
    foreach ($db in $dbFiles) {
        Write-Fail "Database file found in repository: $($db.FullName.Replace($repoRoot, '.'))"
    }
} else {
    Write-Pass "No database files found in repository root."
}

# ─── 5. WAL and SHM Files ───
Write-Host "[5/8] Checking for SQLite journal files..." -ForegroundColor White

$walFiles = Get-ChildItem -Path $repoRoot -Recurse -Include "*-wal","*-shm" -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -notmatch '\\(bin|obj|\.vs|artifacts|TestResults|publish)\\' }

if ($walFiles.Count -gt 0) {
    foreach ($wal in $walFiles) {
        Write-Fail "SQLite journal file found: $($wal.FullName.Replace($repoRoot, '.'))"
    }
} else {
    Write-Pass "No SQLite journal files found."
}

# ─── 6. Common Secret Patterns ───
Write-Host "[6/8] Checking for common secret patterns..." -ForegroundColor White

$secretPatterns = @(
    @{ Pattern = 'api[_-]?key\s*[:=]\s*"[^"]{8,}"'; Name = "API Key assignment" },
    @{ Pattern = 'bearer\s+[a-zA-Z0-9\-_.]{20,}'; Name = "Bearer token" },
    @{ Pattern = 'password\s*[:=]\s*"[^"]{6,}"'; Name = "Hardcoded password" }
)

$secretSafeFiles = @("DbSeeder.cs", "SqliteTestDatabase.cs", "SettingsEditWindow.xaml", "SettingsEditWindow.xaml.cs", "SettingsDetailsWindow.xaml") + (
    Get-ChildItem -Path $repoRoot -Recurse -Include "*Tests.cs" -ErrorAction SilentlyContinue | ForEach-Object { $_.Name }
)

$secretIssueFound = $false
$codeFiles = Get-ChildItem -Path $repoRoot -Recurse -Include "*.cs","*.json","*.xaml" -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -notmatch '\\(bin|obj|\.vs|\.git|artifacts|TestResults)\\' }

foreach ($file in $codeFiles) {
    $content = Get-Content $file.FullName -Raw -ErrorAction SilentlyContinue
    foreach ($sp in $secretPatterns) {
        if ($content -match $sp.Pattern) {
            $baseName = $file.Name
            if ($baseName -notin $secretSafeFiles) {
                Write-Warn "$($file.Name): possible $($sp.Name) detected (review manually)."
                $secretIssueFound = $true
            }
        }
    }
}
if (-not $secretIssueFound) {
    Write-Pass "No unexpected secret patterns found in source code."
}

# ─── 7. Private Credentials Template Check ───
Write-Host "[7/8] Checking gitignore for private credential files..." -ForegroundColor White

$gitignorePath = Join-Path $repoRoot ".gitignore"
if (Test-Path $gitignorePath) {
    $gitignoreContent = Get-Content $gitignorePath -Raw
    if ($gitignoreContent -match "DEMO CREDENTIALS PRIVATE TEMPLATE") {
        Write-Pass "DEMO CREDENTIALS PRIVATE TEMPLATE.md is in .gitignore."
    } else {
        Write-Fail "DEMO CREDENTIALS PRIVATE TEMPLATE.md is NOT in .gitignore."
    }
} else {
    Write-Fail ".gitignore file not found."
}

# ─── 8. Security Documentation Check ───
Write-Host "[8/8] Checking for required security documents..." -ForegroundColor White

$requiredDocs = @(
    "SECURITY CHECKLIST.md",
    "RELEASE SECURITY NOTES.md"
)

foreach ($doc in $requiredDocs) {
    $docPath = Join-Path $repoRoot $doc
    if (Test-Path $docPath) {
        Write-Pass "$doc exists."
    } else {
        Write-Fail "$doc is missing."
    }
}

# ─── Summary ───
Write-Host ""
Write-Host "=============================================" -ForegroundColor Cyan
Write-Host "  Security Scan Summary" -ForegroundColor Cyan
Write-Host "=============================================" -ForegroundColor Cyan
Write-Host "  Passed : $($script:passes)" -ForegroundColor Green
Write-Host "  Warnings: $($script:warnings)" -ForegroundColor Yellow

if ($script:exitCode -ne 0) {
    Write-Host "  RESULT : FAIL — High-risk findings detected." -ForegroundColor Red
} else {
    Write-Host "  RESULT : PASS — No high-risk findings." -ForegroundColor Green
}

Write-Host ""
exit $script:exitCode
