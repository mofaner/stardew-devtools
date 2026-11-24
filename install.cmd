@echo off
setlocal
chcp 65001 >nul 2>&1
title stardew-devtools (sdv-dev) Installer

echo ============================================================
echo   stardew-devtools (sdv-dev) CLI + AI Skill Installer
echo   Supports Windows 10 / Windows 11 (x64)
echo ============================================================
echo.

powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0install.ps1" %*
set "EXIT_CODE=%ERRORLEVEL%"

echo.
if not "%EXIT_CODE%"=="0" (
    echo [ERROR] Installation exited with code %EXIT_CODE%.
    pause
    exit /b %EXIT_CODE%
)

if "%~1"=="" (
    pause
)
exit /b 0
