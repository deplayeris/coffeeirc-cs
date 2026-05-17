@echo off
setlocal enabledelayedexpansion

REM Cake build script runner for CoffeeIRC (Windows)

echo ==========================================
echo CoffeeIRC Cake Build Script
echo ==========================================
echo.

REM Check if dotnet is installed
where dotnet >nul 2>nul
if %errorlevel% neq 0 (
    echo Error: dotnet SDK is not installed.
    echo Please install .NET SDK 10.0 or later from https://dotnet.microsoft.com/download
    exit /b 1
)

for /f "tokens=*" %%i in ('dotnet --version') do set DOTNET_VERSION=%%i
echo Found dotnet: %DOTNET_VERSION%
echo.

REM Install Cake tool if not already installed
where dotnet-cake >nul 2>nul
if %errorlevel% neq 0 (
    echo Installing Cake.Tool...
    dotnet tool install -g Cake.Tool --version 4.0.0
)

REM Run Cake with all arguments passed to this script
echo Running Cake build...
echo.
dotnet cake %*

endlocal
