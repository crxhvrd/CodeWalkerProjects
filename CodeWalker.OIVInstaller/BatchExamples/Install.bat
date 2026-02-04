@echo off
setlocal enabledelayedexpansion

:: ============================================================================
:: OIV Package Installer - Example Install Script
:: ============================================================================
:: This script checks for .oiv files in the same directory and installs one.
:: 
:: HOW TO USE:
:: 1. Place this script in the same folder as your .oiv file(s).
:: 2. Place the 'OIVInstaller' folder (containing CodeWalker.OIVInstaller.exe)
::    anywhere in the parent directory chain (e.g. ../OIVInstaller).
:: 3. Run this script.
::
:: CUSTOMIZATION:
:: - You can hardcode the OIV file name by setting "OIV_FILE=MyMod.oiv" below.
:: - You can hardcode the installer path by setting "INSTALLER=C:\Path\To\Installer.exe".
:: ============================================================================

title OIV Package Installer

:: Get the directory where this script is located
set "SCRIPT_DIR=%~dp0"
set "SCRIPT_DIR=%SCRIPT_DIR:~0,-1%"

:: -----------------------------------------------------------------------------
:: SECTION 1: Detect OIV files
:: -----------------------------------------------------------------------------
:: You can replace this section with: set "OIV_FILE=MyPackage.oiv" to force a specific file.

set "count=0"
for %%f in ("%SCRIPT_DIR%\*.oiv") do (
    set /a count+=1
    set "OIV_!count!=%%~nxf"
    set "OIV_PATH_!count!=%%f"
)

if %count%==0 (
    echo ERROR: No .oiv file found in this folder.
    echo Please place this script next to your .oiv package.
    pause
    exit /b 1
)

if %count%==1 (
    set "OIV_FILE=!OIV_PATH_1!"
    goto :file_selected
)

:: Multiple files found - ask user to choose
echo Multiple OIV packages found:
echo.
for /L %%i in (1,1,%count%) do (
    echo [%%i] !OIV_%%i!
)
echo.

:ask_choice
set /p "choice=Select package to install (1-%count%): "
if not defined choice goto ask_choice
if %choice% LSS 1 goto ask_choice
if %choice% GTR %count% goto ask_choice

set "OIV_FILE=!OIV_PATH_%choice%!"

:file_selected
echo ============================================
echo  OIV Package Installer
echo ============================================
echo.
echo Package: %OIV_FILE%
echo.

:: -----------------------------------------------------------------------------
:: SECTION 2: Locate the OIV Installer Executable
:: -----------------------------------------------------------------------------
:: The script searches for 'CodeWalker.OIVInstaller.exe' in:
:: 1. 'OIVInstaller' subfolder in current or any parent directory (recursive up)
:: 2. The same folder as this script
:: 3. %LOCALAPPDATA%\CodeWalker.OIVInstaller (Default install location)
::
:: To use a custom location, uncomment the line below and set your path:
:: set "INSTALLER=C:\My\Path\To\CodeWalker.OIVInstaller.exe"
:: if exist "%INSTALLER%" goto :found_installer

set "INSTALLER="
set "SEARCH_DIR=%SCRIPT_DIR%"

:find_installer
:: Check for OIVInstaller folder in current search directory
if exist "%SEARCH_DIR%\OIVInstaller\CodeWalker.OIVInstaller.exe" (
    set "INSTALLER=%SEARCH_DIR%\OIVInstaller\CodeWalker.OIVInstaller.exe"
    goto :found_installer
)
:: Check directly in current search directory
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
echo Please ensure the 'OIVInstaller' folder is placed correctly,
echo or install the tool to %LOCALAPPDATA%\CodeWalker.OIVInstaller.
echo.
pause
exit /b 1

:found_installer
:: -----------------------------------------------------------------------------
:: SECTION 3: Run the Installer
:: -----------------------------------------------------------------------------
:: The --install command will open the OIV package.
:: If the game folder is not set in 'cli.json', a Folder Picker dialog will appear.

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
