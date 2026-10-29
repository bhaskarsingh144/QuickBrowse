@echo off
REM QuickBrowse Standalone Setup
REM Compiles and runs QuickBrowse from current directory (no installation required)

echo ========================================
echo QuickBrowse Standalone Setup
echo ========================================
echo.

set SCRIPT_DIR=%~dp0
set ROOT_DIR=%SCRIPT_DIR%..
set BIN_DIR=%ROOT_DIR%\bin
set SRC_DIR=%ROOT_DIR%\src

if not exist "%SRC_DIR%\Program.cs" (
    echo ERROR: Source files not found in %SRC_DIR%
    echo Please ensure you have cloned the repository correctly
    pause
    exit /b 1
)

echo Step 1: Compiling QuickBrowse...
echo.
powershell -NoProfile -ExecutionPolicy Bypass -File "%SCRIPT_DIR%build.ps1"
if %errorLevel% neq 0 (
    echo ERROR: Build failed
    pause
    exit /b 1
)

echo.
echo Step 2: Creating run script...
echo @echo off > "%ROOT_DIR%\run.bat"
echo cd /d "%ROOT_DIR%" >> "%ROOT_DIR%\run.bat"
echo bin\QuickBrowse.exe >> "%ROOT_DIR%\run.bat"

echo.
echo ========================================
echo Setup Complete!
echo ========================================
echo.
echo QuickBrowse has been compiled to: %BIN_DIR%\QuickBrowse.exe
echo.
echo To run QuickBrowse:
echo   - Double-click run.bat
echo   - Or run: bin\QuickBrowse.exe
echo.
echo The application will auto-start on Windows startup after first run.
echo To disable auto-start, delete the shortcut from:
echo   %%APPDATA%%\Microsoft\Windows\Start Menu\Programs\Startup
echo.
echo Starting QuickBrowse now...
start "" "%BIN_DIR%\QuickBrowse.exe"

timeout /t 2 /nobreak >nul
