@echo off
setlocal enabledelayedexpansion

:: ============================================================================
:: OIV Package Installer - Example Uninstall Script
:: ============================================================================
:: This script uninstalls a mod package.
:: It uses the presence of .oiv files to identify WHICH package to uninstall.
:: It reads the metadata inside the .oiv file to get the correct internal Package Name.
::
:: HOW TO USE:
:: 1. Place this script in the same folder as your .oiv file(s).
:: 2. Run the script.
::
:: It ensures that even if you renamed the .oiv file, the correct mod is uninstalled.
:: ============================================================================

title OIV Package Uninstaller

:: Get the directory where this script is located
set "SCRIPT_DIR=%~dp0"
set "SCRIPT_DIR=%SCRIPT_DIR:~0,-1%"

:: -----------------------------------------------------------------------------
:: SECTION 1: Identify Package to Uninstall
:: -----------------------------------------------------------------------------
:: Detects .oiv files to determine what we are uninstalling.

set "count=0"
for %%f in ("%SCRIPT_DIR%\*.oiv") do (
    set /a count+=1
    set "OIV_!count!=%%~nxf"
    set "OIV_PATH_!count!=%%f"
)

if %count%==0 (
    echo ERROR: No .oiv file found in this folder.
    echo This script needs the .oiv file to identify the package name.
    pause
    exit /b 1
)

if %count%==1 (
    set "OIV_FILE=!OIV_PATH_1!"
    goto :file_selected
)

:: Multiple files found - ask user to choose
echo Multiple OIV packages found. Which one do you want to uninstall?
echo.
for /L %%i in (1,1,%count%) do (
    echo [%%i] !OIV_%%i!
)
echo.

:ask_choice
set /p "choice=Select package to uninstall (1-%count%): "
if not defined choice goto ask_choice
if %choice% LSS 1 goto ask_choice
if %choice% GTR %count% goto ask_choice

set "OIV_FILE=!OIV_PATH_%choice%!"

:file_selected
echo ============================================
echo  OIV Package Uninstaller
echo ============================================
echo.
echo Target OIV: %OIV_FILE%
echo.

:: -----------------------------------------------------------------------------
:: SECTION 2: Locate the OIV Installer Executable
:: -----------------------------------------------------------------------------
:: See Install.bat for details on how this search works.

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
:: -----------------------------------------------------------------------------
:: SECTION 3: Run the Uninstaller
:: -----------------------------------------------------------------------------
:: The --uninstall-oiv command reads the OIV file, extracts the package name from metadata,
:: and then uninstalls that package. This is safer than relying on file names.
::
:: OPTIONS:
:: --vanilla : To force a reset to vanilla files instead of restoring backup, add this flag:
:: "%INSTALLER%" --uninstall-oiv "%OIV_FILE%" --vanilla

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
