# Ironbees Test Runner Script
# 테스트 카테고리별 실행 스크립트

param(
    [Parameter(Mandatory=$false)]
    [ValidateSet("all", "unit", "performance", "integration", "ci")]
    [string]$Category = "all",

    [Parameter(Mandatory=$false)]
    [switch]$Coverage
)

$ErrorActionPreference = "Stop"

Write-Host "🐝 Ironbees Test Runner" -ForegroundColor Cyan
Write-Host "Category: $Category" -ForegroundColor Yellow
Write-Host ""

# Base test command
$testCommand = "dotnet test --configuration Debug --verbosity normal"

# Add coverage if requested
if ($Coverage) {
    Write-Host "📊 Code coverage enabled" -ForegroundColor Green
    $testCommand += " --collect:`"XPlat Code Coverage`""
}

# Filter by category
switch ($Category) {
    "all" {
        Write-Host "▶️  Running ALL tests (including Performance tests)" -ForegroundColor White
        # No filter - run everything
    }
    "unit" {
        Write-Host "▶️  Running UNIT tests only" -ForegroundColor White
        $testCommand += " --filter `"Category!=Performance&Category!=Integration`""
    }
    "performance" {
        Write-Host "▶️  Running PERFORMANCE tests only" -ForegroundColor White
        $testCommand += " --filter `"Category=Performance`""
    }
    "integration" {
        Write-Host "▶️  Running INTEGRATION tests only" -ForegroundColor White
        $testCommand += " --filter `"Category=Integration`""
    }
    "ci" {
        Write-Host "▶️  Running CI tests (excluding Performance)" -ForegroundColor White
        $testCommand += " --filter `"Category!=Performance`""
    }
}

Write-Host ""
Write-Host "Command: $testCommand" -ForegroundColor DarkGray
Write-Host ""

# Execute tests
Invoke-Expression $testCommand

if ($LASTEXITCODE -eq 0) {
    Write-Host ""
    Write-Host "✅ Tests completed successfully!" -ForegroundColor Green
} else {
    Write-Host ""
    Write-Host "❌ Tests failed with exit code $LASTEXITCODE" -ForegroundColor Red
    exit $LASTEXITCODE
}
