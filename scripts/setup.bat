@echo off
REM QuickBrowse Setup Script
REM Compiles and installs QuickBrowse system-wide (requires Administrator)

echo ========================================
echo QuickBrowse Setup
echo ========================================
echo.

net session >nul 2>&1
if %errorLevel% neq 0 (
    echo ERROR: This script must be run as Administrator
    echo Right-click the file and select "Run as administrator"
    pause
    exit /b 1
)

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
echo Step 2: Creating installation directory...
set INSTALL_DIR=C:\Program Files\QuickBrowse
if not exist "%INSTALL_DIR%" (
    mkdir "%INSTALL_DIR%"
)

echo Step 3: Copying files...
copy /Y "%BIN_DIR%\QuickBrowse.exe" "%INSTALL_DIR%\" >nul
if %errorLevel% neq 0 (
    echo ERROR: Failed to copy QuickBrowse.exe
    pause
    exit /b 1
)

echo Step 4: Creating uninstall script...
echo @echo off > "%INSTALL_DIR%\uninstall.bat"
echo set INSTALL_DIR=C:\Program Files\QuickBrowse >> "%INSTALL_DIR%\uninstall.bat"
echo echo Uninstalling QuickBrowse... >> "%INSTALL_DIR%\uninstall.bat"
echo taskkill /F /IM QuickBrowse.exe 2>nul >> "%INSTALL_DIR%\uninstall.bat"
echo timeout /t 2 /nobreak >nul >> "%INSTALL_DIR%\uninstall.bat"
echo del "%APPDATA%\Microsoft\Windows\Start Menu\Programs\Startup\QuickBrowse.lnk" 2>nul >> "%INSTALL_DIR%\uninstall.bat"
echo rd /s /q "%LOCALAPPDATA%\QuickBrowse" 2>nul >> "%INSTALL_DIR%\uninstall.bat"
echo cd /d "%INSTALL_DIR%" >> "%INSTALL_DIR%\uninstall.bat"
echo cd .. >> "%INSTALL_DIR%\uninstall.bat"
echo rd /s /q QuickBrowse >> "%INSTALL_DIR%\uninstall.bat"
echo echo QuickBrowse has been uninstalled >> "%INSTALL_DIR%\uninstall.bat"
echo pause >> "%INSTALL_DIR%\uninstall.bat"

echo Step 5: Creating desktop shortcut...
powershell -NoProfile -ExecutionPolicy Bypass -Command "$ws = New-Object -ComObject WScript.Shell; $s = $ws.CreateShortcut('%USERPROFILE%\Desktop\QuickBrowse.lnk'); $s.TargetPath = '%INSTALL_DIR%\QuickBrowse.exe'; $s.WorkingDirectory = '%INSTALL_DIR%'; $s.Description = 'QuickBrowse - Left-edge places browser'; $s.Save()"

echo Step 6: Creating Start Menu shortcut...
powershell -NoProfile -ExecutionPolicy Bypass -Command "$ws = New-Object -ComObject WScript.Shell; $s = $ws.CreateShortcut('%APPDATA%\Microsoft\Windows\Start Menu\Programs\QuickBrowse.lnk'); $s.TargetPath = '%INSTALL_DIR%\QuickBrowse.exe'; $s.WorkingDirectory = '%INSTALL_DIR%'; $s.Description = 'QuickBrowse - Left-edge places browser'; $s.Save()"

echo.
echo ========================================
echo Installation Complete!
echo ========================================
echo.
echo QuickBrowse has been installed to: %INSTALL_DIR%
echo.
echo Starting QuickBrowse...
start "" "%INSTALL_DIR%\QuickBrowse.exe"

timeout /t 3 /nobreak >nul
