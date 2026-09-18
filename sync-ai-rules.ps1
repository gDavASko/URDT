#Requires -Version 5.1
<#
.SYNOPSIS
    Synchronizes AI IDE rules and project-local skills from kbpro-ai-docs into the project root.
.DESCRIPTION
    Source of truth: kbpro-ai-docs/ide-rules/
                     kbpro-ai-docs/all-skills~/ (or kbpro-ai-docs/ai-skills/)
                     kbpro-ai-docs/raw/project-docs/claude-commands/
    Do not edit the target files directly — edit the sources and re-run this script.
#>

$ErrorActionPreference = "Stop"

$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$syncJs = Join-Path $projectRoot "kbpro-ai-docs\system\sync-ai-rules.js"

if ((Get-Command node -ErrorAction SilentlyContinue) -and (Test-Path -LiteralPath $syncJs)) {
    Write-Host "Running authoritative sync engine: node kbpro-ai-docs/system/sync-ai-rules.js..."
    & node $syncJs
    if ($LASTEXITCODE -ne 0) {
        throw "sync-ai-rules.js exited with code $LASTEXITCODE"
    }
    Write-Host "`nAI rules & skills synchronization completed successfully."
    exit 0
}

$ideRulesDir = Join-Path $projectRoot "kbpro-ai-docs\ide-rules"
$aiSkillsDir = if (Test-Path (Join-Path $projectRoot "kbpro-ai-docs\all-skills~")) {
    Join-Path $projectRoot "kbpro-ai-docs\all-skills~"
} else {
    Join-Path $projectRoot "kbpro-ai-docs\ai-skills"
}
$commandsDir  = Join-Path $projectRoot "kbpro-ai-docs\raw\project-docs\claude-commands"

if (-not (Test-Path -LiteralPath $ideRulesDir)) {
    throw "AI rules source directory not found: $ideRulesDir`nRun: git submodule update --init --recursive"
}

$utf8Bom = New-Object System.Text.UTF8Encoding($true)

function Copy-TextFileUtf8Bom {
    param(
        [Parameter(Mandatory)] [string] $Source,
        [Parameter(Mandatory)] [string] $Destination
    )
    if (-not (Test-Path -LiteralPath $Source)) {
        Write-Warning "Source not found, skipping: $Source"
        return
    }
    $destDir = Split-Path -Parent $Destination
    if (-not [string]::IsNullOrWhiteSpace($destDir)) {
        New-Item -ItemType Directory -Force -Path $destDir | Out-Null
    }
    $content = [System.IO.File]::ReadAllText($Source)
    [System.IO.File]::WriteAllText($Destination, $content, $utf8Bom)
    Write-Host "  Synced: $Destination"
}

# ── 1. IDE rule files ──────────────────────────────────────────────────────────
Write-Host "`n[1/3] Syncing IDE rule files from ide-rules/ ..."

$ideFileMap = [ordered]@{
    ".cursorrules"          = ".cursorrules"
    ".windsurfrules"        = ".windsurfrules"
    ".clinerules"           = ".clinerules"
    "GEMINI.md"             = "GEMINI.md"
    "copilot-instructions.md" = ".github\copilot-instructions.md"
    "mcp_config.json"       = "mcp_config.json"
}

foreach ($entry in $ideFileMap.GetEnumerator()) {
    $src = Join-Path $ideRulesDir $entry.Key
    $dst = Join-Path $projectRoot $entry.Value
    Copy-TextFileUtf8Bom -Source $src -Destination $dst
}

# ── 2. Claude Code commands ────────────────────────────────────────────────────
Write-Host "`n[2/3] Syncing Claude Code commands from claude-commands/ ..."

$claudeCommandsDst = Join-Path $projectRoot ".claude\commands"

if (Test-Path -LiteralPath $commandsDir) {
    Get-ChildItem -LiteralPath $commandsDir -Filter "*.md" | ForEach-Object {
        $dst = Join-Path $claudeCommandsDst $_.Name
        Copy-TextFileUtf8Bom -Source $_.FullName -Destination $dst
    }
} else {
    Write-Warning "claude-commands directory not found: $commandsDir"
}

# ── 3. Project-local AI skills ─────────────────────────────────────────────────
Write-Host "`n[3/3] Syncing project-local AI skills from ai-skills/ ..."

if (Test-Path -LiteralPath $aiSkillsDir) {
    Get-ChildItem -LiteralPath $aiSkillsDir -Directory | ForEach-Object {
        $skillName = $_.Name
        $skillSrc  = Join-Path $_.FullName "SKILL.md"

        if (-not (Test-Path -LiteralPath $skillSrc)) {
            Write-Warning "  SKILL.md missing in $skillName, skipping."
            return
        }

        $targets = @(
            ".agents\skills\$skillName\SKILL.md"
            ".codex\skills\$skillName\SKILL.md"
            ".claude\skills\$skillName\SKILL.md"
            ".claude\commands\$skillName.md"
            ".gemini\skills\$skillName\SKILL.md"
            ".cursor\rules\$skillName.mdc"
            ".windsurf\rules\$skillName.md"
            ".cline\rules\$skillName.md"
            ".roo\rules\$skillName.md"
            ".github\instructions\$skillName.instructions.md"
        )

        foreach ($rel in $targets) {
            $dst = Join-Path $projectRoot $rel
            Copy-TextFileUtf8Bom -Source $skillSrc -Destination $dst
        }
    }
} else {
    Write-Warning "ai-skills directory not found: $aiSkillsDir"
}

Write-Host "`nAI-настройка завершена. IDE rules, commands и skills синхронизированы."
Write-Host "Запусти 'git status --short' для проверки изменений."
