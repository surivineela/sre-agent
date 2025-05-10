@echo off
setlocal enabledelayedexpansion

REM Get the directory where this script is located
set SCRIPT_DIR=%~dp0
REM Remove trailing backslash
if "%SCRIPT_DIR:~-1%"=="\" set SCRIPT_DIR=%SCRIPT_DIR:~0,-1%

REM Resolve full path to the Agent folder
set PROJECTS_PATH=%SCRIPT_DIR%\..\..\Agent
for %%I in ("%PROJECTS_PATH%") do set PROJECTS_PATH=%%~fI

REM Project settings
set PROJECT_NAME=FirstPartyAgent.FunctionApp
set PROJECT_PATH=%PROJECTS_PATH%\%PROJECT_NAME%
set PUBLISH_DIR=%PROJECT_PATH%\bin\Release\net9.0\publish

REM 1) Publish the Azure Function App as framework-dependent into the publish folder
echo.
echo ================================
echo Publishing %PROJECT_NAME%...
echo ================================
dotnet publish "%PROJECT_PATH%" ^
    -c Release ^
    -f net9.0 ^
    --output "%PUBLISH_DIR%" ^
    --self-contained false

if ERRORLEVEL 1 (
    echo.
    echo ❌ dotnet publish failed!
    exit /b 1
)

REM 2) Remove unwanted files/folders from the publish output
echo.
echo ================================
echo Cleaning up publish folder...
echo ================================
del /Q "%PUBLISH_DIR%\appsettings.Development.json" 2>nul
rmdir /S /Q "%PUBLISH_DIR%\AlertDetails" 2>nul
rmdir /S /Q "%PUBLISH_DIR%\FirstPartyAgents" 2>nul
rmdir /S /Q "%PUBLISH_DIR%\FirstPartySubAgents" 2>nul
rmdir /S /Q "%PUBLISH_DIR%\ICMAlertConfigs" 2>nul
rmdir /S /Q "%PUBLISH_DIR%\SubAgents" 2>nul
del /Q "%PUBLISH_DIR%\*.zip" 2>nul

REM 3) Create zip archive of the cleaned publish content
echo.
echo ================================
echo Creating deployment.zip...
echo ================================
powershell -Command "Compress-Archive -Path '%PUBLISH_DIR%\*' -DestinationPath '%PROJECT_PATH%\deployment.zip' -Force"

if ERRORLEVEL 1 (
    echo.
    echo ❌ Compression failed!
    exit /b 1
)

echo.
echo ✅ Done. Deployment package is at:
echo    %PROJECT_PATH%\deployment.zip