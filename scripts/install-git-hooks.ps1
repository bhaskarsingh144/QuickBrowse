# Install repo git hooks (strips Cursor co-author trailers on commit).
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$src = Join-Path $root "scripts\git-hooks\commit-msg"
$destDir = Join-Path $root ".git\hooks"
$dest = Join-Path $destDir "commit-msg"
if (-not (Test-Path $src)) {
    Write-Error "Missing $src"
    exit 1
}
if (-not (Test-Path $destDir)) {
    Write-Error "Not a git checkout (missing .git/hooks)"
    exit 1
}
Copy-Item -Path $src -Destination $dest -Force
Write-Host "Installed commit-msg hook -> $dest" -ForegroundColor Green
Write-Host "Also turn off Cursor Settings > Git & PRs > Attribution (Commit + PR)." -ForegroundColor Yellow
