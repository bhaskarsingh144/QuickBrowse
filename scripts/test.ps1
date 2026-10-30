# Run QuickBrowse unit tests (no NuGet).
$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$rootDir = Split-Path -Parent $scriptDir
$srcDir = Join-Path $rootDir "src"
$testsDir = Join-Path $rootDir "tests"
$outputDir = Join-Path $rootDir "bin"

if (-not (Test-Path $outputDir)) {
    New-Item -ItemType Directory -Path $outputDir | Out-Null
}

$fw = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319"
$csc = Join-Path $fw "csc.exe"
if (-not (Test-Path $csc)) {
    Write-Error "C# compiler not found at $csc"
    exit 1
}

function Run-Suite([string]$name, [string[]]$sources, [string]$exeName, [string[]]$refs = @()) {
    foreach ($f in $sources) {
        if (-not (Test-Path $f)) {
            Write-Error "Missing source: $f"
            exit 1
        }
    }
    $exePath = Join-Path $outputDir $exeName
    Write-Host "Compiling $name..." -ForegroundColor Cyan
    $args = @("/nologo", "/optimize", "/target:exe", "/out:$exePath")
    foreach ($r in $refs) {
        $args += "/r:$r"
    }
    $args += $sources
    & $csc @args
    if ($LASTEXITCODE -ne 0) {
        Write-Error "$name compile failed"
        exit 1
    }
    Write-Host "Running $exePath" -ForegroundColor Cyan
    & $exePath
    if ($LASTEXITCODE -ne 0) {
        Write-Host "$name failed (exit $LASTEXITCODE)." -ForegroundColor Red
        exit $LASTEXITCODE
    }
    Write-Host "$name passed." -ForegroundColor Green
}

$drawing = @(
    "System.Drawing.dll",
    "System.Windows.Forms.dll",
    "System.Management.dll"
)

$core = @("System.dll", "System.Core.dll")

Run-Suite "DisplayScaleTests" @(
    (Join-Path $srcDir "DisplayScale.cs"),
    (Join-Path $testsDir "DisplayScaleTests.cs")
) "DisplayScaleTests.exe" $drawing

Run-Suite "DirectoryModelTests" @(
    (Join-Path $srcDir "DirectoryModel.cs"),
    (Join-Path $testsDir "DirectoryModelTests.cs")
) "DirectoryModelTests.exe" $core

Run-Suite "PathCrumbsTests" @(
    (Join-Path $srcDir "PathCrumbs.cs"),
    (Join-Path $testsDir "PathCrumbsTests.cs")
) "PathCrumbsTests.exe" $drawing

Run-Suite "PlacesCatalogTests" @(
    (Join-Path $srcDir "PlacesCatalog.cs"),
    (Join-Path $testsDir "PlacesCatalogTests.cs")
) "PlacesCatalogTests.exe" $core

Run-Suite "ChromeLayoutTests" @(
    (Join-Path $srcDir "DisplayScale.cs"),
    (Join-Path $srcDir "ChromeLayout.cs"),
    (Join-Path $testsDir "ChromeLayoutTests.cs")
) "ChromeLayoutTests.exe" $drawing

Run-Suite "RailMetricsTests" @(
    (Join-Path $srcDir "DisplayScale.cs"),
    (Join-Path $srcDir "ChromeLayout.cs"),
    (Join-Path $testsDir "RailMetricsTests.cs")
) "RailMetricsTests.exe" $drawing

Run-Suite "PrefsTests" @(
    (Join-Path $srcDir "Prefs.cs"),
    (Join-Path $testsDir "PrefsTests.cs")
) "PrefsTests.exe" $core

Run-Suite "PinStoreTests" @(
    (Join-Path $srcDir "PinStore.cs"),
    (Join-Path $testsDir "PinStoreTests.cs")
) "PinStoreTests.exe" $core

Run-Suite "PinSessionTests" @(
    (Join-Path $srcDir "PinStore.cs"),
    (Join-Path $srcDir "PinSession.cs"),
    (Join-Path $testsDir "PinSessionTests.cs")
) "PinSessionTests.exe" $core

Run-Suite "RecentSourceTests" @(
    (Join-Path $srcDir "PinStore.cs"),
    (Join-Path $srcDir "RecentSource.cs"),
    (Join-Path $testsDir "RecentSourceTests.cs")
) "RecentSourceTests.exe" $core

Run-Suite "ShellContextMenuTests" @(
    (Join-Path $srcDir "ShellContextMenu.cs"),
    (Join-Path $testsDir "ShellContextMenuTests.cs")
) "ShellContextMenuTests.exe" $drawing

Write-Host "All tests passed." -ForegroundColor Green
