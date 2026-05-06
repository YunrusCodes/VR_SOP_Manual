# codex-image-gen skill installer (Windows / PowerShell)
#
# Usage (from the unzipped skill folder):
#     .\install.ps1                    # interactive: asks user-level vs project-local
#     .\install.ps1 -Scope user        # install to %USERPROFILE%\.claude\skills\
#     .\install.ps1 -Scope project     # install to <cwd>\.claude\skills\
#     .\install.ps1 -Scope project -ProjectDir C:\path\to\repo
#
# The script:
#   1. Checks Python (prefers `py`, falls back to python3/python)
#   2. Checks codex CLI on PATH
#   3. Copies skill files to the chosen target
#   4. Smoke test (--help on generate.py)

[CmdletBinding()]
param(
    [ValidateSet('user', 'project', 'ask')]
    [string]$Scope = 'ask',

    [string]$ProjectDir = (Get-Location).Path,

    [switch]$SkipDeps
)

$ErrorActionPreference = 'Stop'
$SkillName = 'codex-image-gen'
$SrcDir = $PSScriptRoot

Write-Host "=== codex-image-gen installer ===" -ForegroundColor Cyan
Write-Host "Source: $SrcDir"
Write-Host ""

function Test-Command($name) {
    return [bool](Get-Command $name -ErrorAction SilentlyContinue)
}

# --- 1. Dependency checks --------------------------------------------------

if (-not $SkipDeps) {
    Write-Host "[1/3] Checking dependencies..."

    $py = $null
    foreach ($candidate in @('py', 'python3', 'python')) {
        if (Test-Command $candidate) {
            $version = & $candidate --version 2>&1
            if ($LASTEXITCODE -eq 0) {
                $py = $candidate
                break
            }
        }
    }
    if (-not $py) {
        Write-Error "Python not found. Install Python 3.9+ from https://python.org or via winget: winget install Python.Python.3.12"
    }
    Write-Host "  Python: $py ($(& $py --version 2>&1))" -ForegroundColor Green

    if (-not (Test-Command 'codex')) {
        Write-Error "codex CLI not found on PATH. Install from https://github.com/openai/codex first, then re-run this installer."
    }
    Write-Host "  Codex CLI: detected" -ForegroundColor Green
} else {
    Write-Host "[1/3] Skipping dependency checks (--SkipDeps)" -ForegroundColor Yellow
}

# --- 2. Pick target directory ----------------------------------------------

Write-Host ""
Write-Host "[2/3] Choosing install location..."

if ($Scope -eq 'ask') {
    Write-Host ""
    Write-Host "Where should the skill be installed?"
    Write-Host "  [1] User-level   ($env:USERPROFILE\.claude\skills\$SkillName)"
    Write-Host "      All your projects can trigger it. Recommended for personal use."
    Write-Host "  [2] Project-local ($ProjectDir\.claude\skills\$SkillName)"
    Write-Host "      Only the project at $ProjectDir can trigger it. Good for team-shared repos."
    Write-Host ""
    do {
        $choice = Read-Host "Choice (1 or 2)"
    } while ($choice -ne '1' -and $choice -ne '2')
    $Scope = if ($choice -eq '1') { 'user' } else { 'project' }
}

$Target = if ($Scope -eq 'user') {
    Join-Path $env:USERPROFILE ".claude\skills\$SkillName"
} else {
    Join-Path $ProjectDir ".claude\skills\$SkillName"
}

Write-Host "  Target: $Target" -ForegroundColor Green

# --- 3. Copy files ---------------------------------------------------------

Write-Host ""
Write-Host "[3/3] Copying files..."

if (Test-Path $Target) {
    Write-Host "  Target exists. Overwriting." -ForegroundColor Yellow
    Remove-Item -Path $Target -Recurse -Force
}
New-Item -Path $Target -ItemType Directory -Force | Out-Null

$skip = @('install.ps1', 'install.sh', '__pycache__')
Get-ChildItem -Path $SrcDir | Where-Object { $skip -notcontains $_.Name } | ForEach-Object {
    Copy-Item -Path $_.FullName -Destination $Target -Recurse -Force
}

Write-Host "  Copied to: $Target" -ForegroundColor Green

# --- 4. Smoke test ---------------------------------------------------------

if (-not $SkipDeps) {
    Write-Host ""
    Write-Host "Running smoke test (generate.py --help)..."

    $smokeResult = & $py "$Target\generate.py" --help 2>&1
    if ($LASTEXITCODE -eq 0) {
        Write-Host "  Smoke test PASSED" -ForegroundColor Green
    } else {
        Write-Host "  Smoke test FAILED:" -ForegroundColor Red
        Write-Host $smokeResult
    }
}

Write-Host ""
Write-Host "=== Install complete ===" -ForegroundColor Cyan
Write-Host ""
Write-Host "To use the skill in Claude Code, just ask for an image:"
Write-Host '  "Draw me a watercolor fox in a forest"' -ForegroundColor Gray
Write-Host '  "Generate a cyberpunk city skyline, widescreen"' -ForegroundColor Gray
Write-Host '  "Make this photo black and white"  (with attached image)' -ForegroundColor Gray
Write-Host ""
Write-Host "Or run the orchestrator manually:"
Write-Host "  py `"$Target\generate.py`" `"a watercolor fox`" --output fox.png" -ForegroundColor Gray
Write-Host ""
