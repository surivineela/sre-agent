@echo off
setlocal enabledelayedexpansion

REM === Accept ACR name as argument ===
if "%~1"=="" (
    echo ERROR: ACR name not provided.
    exit /b 1
)
set REGISTRY_NAME=%~1
set IMAGE_NAME=%REGISTRY_NAME%.azurecr.io/fpagentweb:latest

REM Get the directory where this script is located
set SCRIPT_DIR=%~dp0
REM Remove trailing backslash
if "%SCRIPT_DIR:~-1%"=="\" set SCRIPT_DIR=%SCRIPT_DIR:~0,-1%

REM Set variables
set SCRIPT_DIR=%~dp0
if "%SCRIPT_DIR:~-1%"=="\" set SCRIPT_DIR=%SCRIPT_DIR:~0,-1%

set PROJECTS_PATH=%SCRIPT_DIR%\..\..\Agent
for %%I in ("%PROJECTS_PATH%") do set PROJECTS_PATH=%%~fI

set PROJECT_NAME=FirstPartyAgent.Web
set PROJECT_PATH=%PROJECTS_PATH%\%PROJECT_NAME%
set CSPROJ_FILE=%PROJECT_PATH%\%PROJECT_NAME%.csproj
set PUBLISH_DIR=%PROJECTS_PATH%\publish
set CONTAINER_NAME=fpagentwebcontainer

echo SCRIPT_DIR=%SCRIPT_DIR%
echo PROJECTS_PATH=%PROJECTS_PATH%
echo PROJECT_NAME=%PROJECT_NAME%
echo PROJECT_PATH=%PROJECT_PATH%
echo CSPROJ_FILE=%CSPROJ_FILE%
echo PUBLISH_DIR=%PUBLISH_DIR%
echo IMAGE_NAME=%IMAGE_NAME%

REM Step 1: Clean previous publish
rmdir /S /Q "%PUBLISH_DIR%"
mkdir "%PUBLISH_DIR%"

REM Step 2: Build the project
dotnet build "%CSPROJ_FILE%" --configuration Release

REM Step 3: Publish the project (excluding appsettings.Development.json)
dotnet publish "%CSPROJ_FILE%" --configuration Release --output "%PUBLISH_DIR%" /p:ExcludeFilesFromPublish=appsettings.Development.json

REM Step 4: Copy Dockerfile into publish folder (optional - if Dockerfile is outside project folder)
copy "%SCRIPT_DIR%\Dockerfile" "%PUBLISH_DIR%"

REM Step 5: Build Docker image
docker build -t %IMAGE_NAME% "%PUBLISH_DIR%"

REM Login to ACR
az acr login --name %REGISTRY_NAME%
if errorlevel 1 (
    echo Failed to login to ACR.
    exit /b 1
)

REM Confirm image exists
docker images | findstr %REGISTRY_NAME%

REM Push the image
echo.
echo Pushing the image to ACR...
docker push %IMAGE_NAME%
if errorlevel 1 (
    echo Docker push failed.
    exit /b 1
)