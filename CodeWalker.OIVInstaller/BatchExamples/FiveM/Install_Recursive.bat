@echo off
setlocal enabledelayedexpansion

:: ============================================================================
:: FiveM Mod Installer - Install Script (Recursive)
:: ============================================================================
:: This script will recursively find the first .rpf file in this folder 
:: (or subfolders) and install it to %localappdata%\FiveM\FiveM.app\mods
:: ============================================================================

title FiveM Mod Installer

:: Get the directory where this script is located
set "SCRIPT_DIR=%~dp0"
set "SCRIPT_DIR=%SCRIPT_DIR:~0,-1%"

:: -----------------------------------------------------------------------------
:: Detect RPF files (Recursive)
:: -----------------------------------------------------------------------------
set "count=0"
for /r "%SCRIPT_DIR%" %%f in (*.rpf) do (
    set /a count+=1
    set "RPF_!count!=%%~nxf"
    set "RPF_PATH_!count!=%%f"
)

if %count%==0 (
    echo ERROR: No .rpf file found in this folder or subfolders.
    pause
    exit /b 1
)

if %count%==1 (
    set "RPF_FILE=!RPF_PATH_1!"
    goto :file_selected
)

:: Multiple files found - ask user to choose
echo Multiple RPF packages found:
echo.
for /L %%i in (1,1,%count%) do (
    echo [%%i] !RPF_%%i!
)
echo.

:ask_choice
set /p "choice=Select package to install (1-%count%): "
if not defined choice goto ask_choice
if %choice% LSS 1 goto ask_choice
if %choice% GTR %count% goto ask_choice

set "RPF_FILE=!RPF_PATH_%choice%!"

:file_selected
echo ============================================
echo  FiveM Mod Installer
echo ============================================
echo.
echo Package: %RPF_FILE%
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
if "%PARENT_DIR%"=="%SEARCH_DIR%" goto :installer_not_found
set "SEARCH_DIR=%PARENT_DIR%"
goto :find_installer

:installer_not_found
echo ERROR: CodeWalker.OIVInstaller.exe not found!
echo Please ensure the OIVInstaller folder exists.
pause
exit /b 1

:found_installer
:: Run the installer
"%INSTALLER%" --install "%RPF_FILE%"
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
