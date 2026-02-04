@echo off
setlocal enabledelayedexpansion

:: ============================================================================
:: OIV Package Installer - Example Uninstall Script
:: ============================================================================
:: This script uses the .oiv file name to identify and uninstall the package.
:: It extracts the real package name from the OIV metadata (via --uninstall-oiv).

title OIV Package Uninstaller

:: Get the directory where this script is located
set "SCRIPT_DIR=%~dp0"
set "SCRIPT_DIR=%SCRIPT_DIR:~0,-1%"

:: Find the OIV file to determine which package to uninstall
set "OIV_FILE="
for %%f in ("%SCRIPT_DIR%\*.oiv") do (
    set "OIV_FILE=%%f"
)

if not defined OIV_FILE (
    echo ERROR: No .oiv file found in this folder.
    echo This script needs the .oiv file to know which package to uninstall.
    pause
    exit /b 1
)

echo ============================================
echo  OIV Package Uninstaller
echo ============================================
echo.
echo OIV Package: %OIV_FILE%
echo.

:: -----------------------------------------------------------------------------
:: Locate the OIV Installer Executable
:: -----------------------------------------------------------------------------

set "INSTALLER="
set "SEARCH_DIR=%SCRIPT_DIR%"

:find_installer
if exist "%SEARCH_DIR%\OIVInstaller\CodeWalker.OIVInstaller.exe" (
    set "INSTALLER=%SEARCH_DIR%\OIVInstaller\CodeWalker.OIVInstaller.exe"
    goto :found_installer
)
if exist "%SEARCH_DIR%\CodeWalker.OIVInstaller.exe" (
    set "INSTALLER=%SEARCH_DIR%\CodeWalker.OIVInstaller.exe"
    goto :found_installer
)

for %%i in ("%SEARCH_DIR%\..") do set "PARENT_DIR=%%~fi"
if "%PARENT_DIR%"=="%SEARCH_DIR%" goto :check_local_appdata
set "SEARCH_DIR=%PARENT_DIR%"
goto :find_installer

:check_local_appdata
if exist "%LOCALAPPDATA%\CodeWalker.OIVInstaller\CodeWalker.OIVInstaller.exe" (
    set "INSTALLER=%LOCALAPPDATA%\CodeWalker.OIVInstaller\CodeWalker.OIVInstaller.exe"
    goto :found_installer
)

:installer_not_found
echo ERROR: CodeWalker.OIVInstaller.exe not found!
pause
exit /b 1

:found_installer
:: Run uninstall using the OIV file path
:: The installer will read the OIV metadata to get the correct package ID/name.
"%INSTALLER%" --uninstall-oiv "%OIV_FILE%"
set "RESULT=%ERRORLEVEL%"

echo.
if %RESULT%==0 (
    echo ============================================
    echo  Uninstall completed successfully!
    echo ============================================
) else (
    echo ============================================
    echo  Uninstall failed with error code: %RESULT%
    echo ============================================
)

echo.
pause
exit /b %RESULT%
