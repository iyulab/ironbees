<![CDATA[#Requires -Version 5.1

<#
.SYNOPSIS
    Migrates ironbees namespace from v0.1.8 to v0.4.1 structure.

.DESCRIPTION
    This script automates the migration of ironbees namespaces from the old structure
    (Ironbees.AgentMode.Core.*) to the new consolidated structure (Ironbees.AgentMode.*).

    It performs the following operations:
    1. Creates backup of all .cs files
    2. Replaces old namespace imports with new ones
    3. Generates detailed migration report
    4. Optionally validates changes

.PARAMETER ProjectPath
    Path to the project root directory. Defaults to current directory.

.PARAMETER DryRun
    If specified, shows what would be changed without modifying files.

.PARAMETER NoBackup
    If specified, skips creating .bak backup files (not recommended).

.PARAMETER Validate
    If specified, runs 'dotnet build' after migration to validate changes.

.EXAMPLE
    .\migrate-namespaces.ps1 -DryRun
    Preview changes without modifying files.

.EXAMPLE
    .\migrate-namespaces.ps1 -ProjectPath .\src\MyProject
    Migrate namespaces in specific project directory.

.EXAMPLE
    .\migrate-namespaces.ps1 -Validate
    Migrate and validate with 'dotnet build'.

.NOTES
    Author: ironbees Team
    Version: 1.0.0
    Last Updated: 2026-01-06
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [string]$ProjectPath = ".",

    [Parameter(Mandatory = $false)]
    [switch]$DryRun,

    [Parameter(Mandatory = $false)]
    [switch]$NoBackup,

    [Parameter(Mandatory = $false)]
    [switch]$Validate
)

# Namespace migration mappings
$namespaceMappings = @(
    # Workflow namespace - requires two using statements
    @{
        Pattern = 'using\s+Ironbees\.AgentMode\.Core\.Workflow\s*;'
        Replacement = "using Ironbees.AgentMode.Workflow;`nusing Ironbees.AgentMode.Models;"
        Description = "Workflow namespace (split into Workflow + Models)"
    },
    # Core.Models -> Models
    @{
        Pattern = 'using\s+Ironbees\.AgentMode\.Core\.Models\s*;'
        Replacement = 'using Ironbees.AgentMode.Models;'
        Description = "Models namespace"
    },
    # Core.Agents -> Agents
    @{
        Pattern = 'using\s+Ironbees\.AgentMode\.Core\.Agents\s*;'
        Replacement = 'using Ironbees.AgentMode.Agents;'
        Description = "Agents namespace"
    },
    # Core.Workflow.Triggers -> Workflow.Triggers
    @{
        Pattern = 'using\s+Ironbees\.AgentMode\.Core\.Workflow\.Triggers\s*;'
        Replacement = 'using Ironbees.AgentMode.Workflow.Triggers;'
        Description = "Workflow.Triggers namespace"
    },
    # Core (generic) -> AgentMode
    @{
        Pattern = 'using\s+Ironbees\.AgentMode\.Core\s*;'
        Replacement = 'using Ironbees.AgentMode;'
        Description = "Core namespace"
    },
    # Providers -> Microsoft.Extensions.AI + OpenAI
    @{
        Pattern = 'using\s+Ironbees\.AgentMode\.Providers\s*;'
        Replacement = "using Microsoft.Extensions.AI;`nusing OpenAI;"
        Description = "Providers namespace (replaced with ME.AI)"
    }
)

# Statistics
$stats = @{
    FilesProcessed = 0
    FilesChanged = 0
    NamespacesMigrated = 0
    BackupsCreated = 0
    Errors = @()
}

function Write-Header {
    Write-Host ""
    Write-Host "=== Namespace Migration Tool ===" -ForegroundColor Cyan
    Write-Host "Project Path: $((Resolve-Path $ProjectPath).Path)" -ForegroundColor Gray
    Write-Host "Mode: $(if ($DryRun) { 'Dry Run (Preview)' } else { 'Live Migration' })" -ForegroundColor Gray
    Write-Host ""
}

function Get-CSharpFiles {
    param([string]$Path)

    Get-ChildItem -Path $Path -Filter "*.cs" -Recurse -File |
        Where-Object {
            $_.FullName -notmatch '\\obj\\' -and
            $_.FullName -notmatch '\\bin\\' -and
            $_.FullName -notmatch '\\.vs\\'
        }
}

function Backup-File {
    param([System.IO.FileInfo]$File)

    if ($NoBackup -or $DryRun) {
        return $true
    }

    try {
        $backupPath = "$($File.FullName).bak"
        Copy-Item -Path $File.FullName -Destination $backupPath -Force
        $stats.BackupsCreated++
        return $true
    }
    catch {
        Write-Warning "Failed to backup $($File.Name): $_"
        $stats.Errors += "Backup failed: $($File.Name)"
        return $false
    }
}

function Migrate-Namespaces {
    param([System.IO.FileInfo]$File)

    try {
        $content = Get-Content -Path $File.FullName -Raw
        $originalContent = $content
        $changesMade = 0

        foreach ($mapping in $namespaceMappings) {
            if ($content -match $mapping.Pattern) {
                $content = $content -replace $mapping.Pattern, $mapping.Replacement
                $changesMade++
                $stats.NamespacesMigrated++
            }
        }

        if ($changesMade -gt 0) {
            $stats.FilesChanged++

            if ($DryRun) {
                Write-Host "  Would update: $($File.Name) ($changesMade namespaces)" -ForegroundColor Yellow
            }
            else {
                # Backup first
                if (-not (Backup-File -File $File)) {
                    return $false
                }

                # Write updated content
                Set-Content -Path $File.FullName -Value $content -NoNewline
                Write-Host "  Updated: $($File.Name) ($changesMade namespaces)" -ForegroundColor Green
            }

            return $true
        }

        return $false
    }
    catch {
        Write-Warning "Failed to process $($File.Name): $_"
        $stats.Errors += "Processing failed: $($File.Name) - $_"
        return $false
    }
}

function Remove-DuplicateUsings {
    param([System.IO.FileInfo]$File)

    if ($DryRun) { return }

    try {
        $content = Get-Content -Path $File.FullName -Raw

        # Simple duplicate removal (consecutive duplicates)
        $lines = $content -split "`r?`n"
        $uniqueLines = @()
        $previousLine = $null

        foreach ($line in $lines) {
            if ($line -match '^\s*using\s+' -and $line -eq $previousLine) {
                # Skip duplicate using statement
                continue
            }
            $uniqueLines += $line
            $previousLine = $line
        }

        $cleanedContent = $uniqueLines -join "`n"
        if ($cleanedContent -ne $content) {
            Set-Content -Path $File.FullName -Value $cleanedContent -NoNewline
        }
    }
    catch {
        Write-Verbose "Failed to clean duplicates in $($File.Name): $_"
    }
}

function Show-Summary {
    Write-Host ""
    Write-Host "=== Migration Summary ===" -ForegroundColor Cyan
    Write-Host "Files processed:      $($stats.FilesProcessed)" -ForegroundColor Gray
    Write-Host "Files changed:        $($stats.FilesChanged)" -ForegroundColor $(if ($stats.FilesChanged -gt 0) { 'Green' } else { 'Gray' })
    Write-Host "Namespaces migrated:  $($stats.NamespacesMigrated)" -ForegroundColor $(if ($stats.NamespacesMigrated -gt 0) { 'Green' } else { 'Gray' })

    if (-not $DryRun -and -not $NoBackup) {
        Write-Host "Backups created:      $($stats.BackupsCreated) (.bak files)" -ForegroundColor Gray
    }

    if ($stats.Errors.Count -gt 0) {
        Write-Host ""
        Write-Host "Errors encountered:   $($stats.Errors.Count)" -ForegroundColor Red
        foreach ($error in $stats.Errors) {
            Write-Host "  - $error" -ForegroundColor Red
        }
    }

    Write-Host ""

    if ($DryRun) {
        Write-Host "This was a dry run. No files were modified." -ForegroundColor Yellow
        Write-Host "Run without -DryRun to apply changes." -ForegroundColor Yellow
    }
    elseif ($stats.FilesChanged -gt 0) {
        Write-Host "Migration completed successfully!" -ForegroundColor Green
        Write-Host ""
        Write-Host "Next steps:" -ForegroundColor Cyan
        Write-Host "1. Review changes (use git diff)" -ForegroundColor Gray
        Write-Host "2. Run 'dotnet build' to verify" -ForegroundColor Gray
        Write-Host "3. Run 'dotnet test' to validate" -ForegroundColor Gray
        if (-not $NoBackup) {
            Write-Host "4. Delete .bak files when satisfied" -ForegroundColor Gray
        }
    }
    else {
        Write-Host "No namespace migrations needed." -ForegroundColor Gray
    }
}

function Invoke-Build {
    Write-Host ""
    Write-Host "Running 'dotnet build' to validate changes..." -ForegroundColor Cyan

    try {
        $output = dotnet build 2>&1
        $exitCode = $LASTEXITCODE

        if ($exitCode -eq 0) {
            Write-Host "Build succeeded!" -ForegroundColor Green
            return $true
        }
        else {
            Write-Host "Build failed with errors:" -ForegroundColor Red
            $output | ForEach-Object { Write-Host $_ -ForegroundColor Red }
            return $false
        }
    }
    catch {
        Write-Warning "Failed to run build: $_"
        return $false
    }
}

# Main execution
try {
    $startTime = Get-Date

    Write-Header

    # Validate project path
    if (-not (Test-Path $ProjectPath)) {
        Write-Error "Project path not found: $ProjectPath"
        exit 1
    }

    # Get all C# files
    Write-Host "Scanning for C# files..." -ForegroundColor Gray
    $csFiles = Get-CSharpFiles -Path $ProjectPath
    $stats.FilesProcessed = $csFiles.Count

    if ($stats.FilesProcessed -eq 0) {
        Write-Warning "No C# files found in $ProjectPath"
        exit 0
    }

    Write-Host "Found $($stats.FilesProcessed) C# files." -ForegroundColor Gray
    Write-Host ""

    # Migrate namespaces
    Write-Host "Migrating namespaces..." -ForegroundColor Cyan

    foreach ($file in $csFiles) {
        $migrated = Migrate-Namespaces -File $file

        # Clean up duplicate usings
        if ($migrated -and -not $DryRun) {
            Remove-DuplicateUsings -File $file
        }
    }

    $duration = (Get-Date) - $startTime

    # Show summary
    Show-Summary

    Write-Host "Duration: $($duration.TotalSeconds.ToString('0.0'))s" -ForegroundColor Gray

    # Optional validation
    if ($Validate -and -not $DryRun -and $stats.FilesChanged -gt 0) {
        $buildSuccess = Invoke-Build
        if (-not $buildSuccess) {
            Write-Warning "Build validation failed. Review errors above."
            exit 1
        }
    }

    exit 0
}
catch {
    Write-Error "Migration failed: $_"
    Write-Error $_.ScriptStackTrace
    exit 1
}
]]>