@echo off
setlocal enabledelayedexpansion

:: ============================================================================
:: OIV Package Installer - Example Install Script
:: ============================================================================
:: This script checks for an .oiv file in the same directory and installs it.
:: It automatically finds the CodeWalker.OIVInstaller.exe in common locations.

title OIV Package Installer

:: Get the directory where this script is located
set "SCRIPT_DIR=%~dp0"
set "SCRIPT_DIR=%SCRIPT_DIR:~0,-1%"

:: Find the OIV file in the same directory
set "OIV_FILE="
for %%f in ("%SCRIPT_DIR%\*.oiv") do (
    set "OIV_FILE=%%f"
)

if not defined OIV_FILE (
    echo ERROR: No .oiv file found in this folder.
    echo Please place this script next to your .oiv package.
    pause
    exit /b 1
)

echo ============================================
echo  OIV Package Installer
echo ============================================
echo.
echo Package: %OIV_FILE%
echo.

:: -----------------------------------------------------------------------------
:: Locate the OIV Installer Executable
:: Tries:
:: 1. OIVInstaller subfolder in current or parent directories (recursive up)
:: 2. Same folder as script
:: 3. LocalAppData default install location
:: -----------------------------------------------------------------------------

set "INSTALLER="
set "SEARCH_DIR=%SCRIPT_DIR%"

:find_installer
:: Check for OIVInstaller/CodeWalker.OIVInstaller.exe (Standard distribution structure)
if exist "%SEARCH_DIR%\OIVInstaller\CodeWalker.OIVInstaller.exe" (
    set "INSTALLER=%SEARCH_DIR%\OIVInstaller\CodeWalker.OIVInstaller.exe"
    goto :found_installer
)
:: Check in same folder (if script is placed next to exe)
if exist "%SEARCH_DIR%\CodeWalker.OIVInstaller.exe" (
    set "INSTALLER=%SEARCH_DIR%\CodeWalker.OIVInstaller.exe"
    goto :found_installer
)

:: Go up one directory to continue search
for %%i in ("%SEARCH_DIR%\..") do set "PARENT_DIR=%%~fi"
if "%PARENT_DIR%"=="%SEARCH_DIR%" goto :check_local_appdata
set "SEARCH_DIR=%PARENT_DIR%"
goto :find_installer

:check_local_appdata
:: Check generic installation location
if exist "%LOCALAPPDATA%\CodeWalker.OIVInstaller\CodeWalker.OIVInstaller.exe" (
    set "INSTALLER=%LOCALAPPDATA%\CodeWalker.OIVInstaller\CodeWalker.OIVInstaller.exe"
    goto :found_installer
)

:installer_not_found
echo ERROR: CodeWalker.OIVInstaller.exe not found!
echo.
echo Could not find the installer in any of these locations:
echo 1. ..\OIVInstaller\CodeWalker.OIVInstaller.exe (in any parent folder)
echo 2. CodeWalker.OIVInstaller.exe (in same folder)
echo 3. %%LOCALAPPDATA%%\CodeWalker.OIVInstaller\CodeWalker.OIVInstaller.exe
echo.
pause
exit /b 1

:found_installer
:: Run the installer
:: It will auto-prompt for the game folder (folder picker) if not already configured.
"%INSTALLER%" --install "%OIV_FILE%"
set "RESULT=%ERRORLEVEL%"

echo.
if %RESULT%==0 (
    echo ============================================
    echo  Installation completed successfully!
    echo ============================================
) else (
    echo ============================================
    echo  Installation failed with error code: %RESULT%
    echo ============================================
)

echo.
pause
exit /b %RESULT%
