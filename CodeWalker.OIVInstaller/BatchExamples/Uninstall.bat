@echo off
setlocal enabledelayedexpansion

:: ============================================================================
:: OIV Package Installer - Uninstall Script
:: ============================================================================

title OIV Package Uninstaller

:: Get the directory where this script is located
set "SCRIPT_DIR=%~dp0"
set "SCRIPT_DIR=%SCRIPT_DIR:~0,-1%"

:: -----------------------------------------------------------------------------
:: Detect OIV files
:: -----------------------------------------------------------------------------
set "count=0"
for %%f in ("%SCRIPT_DIR%\*.oiv") do (
    set /a count+=1
    set "OIV_!count!=%%~nxf"
    set "OIV_PATH_!count!=%%f"
)

if %count%==0 (
    echo ERROR: No .oiv file found in this folder.
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
if "%PARENT_DIR%"=="%SEARCH_DIR%" goto :installer_not_found
set "SEARCH_DIR=%PARENT_DIR%"
goto :find_installer

:installer_not_found
echo ERROR: CodeWalker.OIVInstaller.exe not found!
echo Please ensure the OIVInstaller folder exists.
pause
exit /b 1

:found_installer
:: Run the uninstaller with OIV path
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
