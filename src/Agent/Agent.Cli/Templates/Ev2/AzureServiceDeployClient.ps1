#-------------------------------------------------------------------------------------
# <copyright file="AzureServiceDeployClient.ps1" company="Microsoft">
#     Copyright (c) Microsoft Corporation.  All rights reserved.
# </copyright>
#
# <Summary>
#     AzureServiceDeploy Powershell command console startup script.
# </Summary>
#-------------------------------------------------------------------------------------
param(
    [bool]$fromShortcut,
    [bool]$skipScriptUpdate
)

[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

$ev2NugetSource = "https://msazure.pkgs.visualstudio.com/_packaging/ExpressV2/nuget/v3/index.json"

$startupScriptPkgName = "Microsoft.Azure.AzureServiceDeployClientStartup"
$sdkPackageName = "Microsoft.Azure.AzureServiceDeployClient"

function CheckScriptUpdate
{
    try
    {
        $scriptPackagePath = Join-Path $azureServiceDeployClientPath "AzureServiceDeployClientStartup"
        $startupPkgVersionFile = Join-Path $azureServiceDeployClientPath "AzureServiceDeployClientStartup_version.txt"
        $found = $false

        EnsureDependencyPresence

        # Query latest version and check with the current version of the startup script.
        $latestPkgVer = GetLatestPackageVersion $startupScriptPkgName $ev2NugetSource

        if (Test-Path $startupPkgVersionFile)
        {
            $installedVersion = Get-Content $startupPkgVersionFile
            $found = $installedVersion -eq $latestPkgVer
        }

        if (!$found)
        {
            Write-Host "Latest startup script not found. Downloading latest startup package $startupScriptPkgName."
            DownloadStartupScriptPackage $scriptPackagePath $latestPkgVer

            $scriptPackageLibPath = "$scriptPackagePath\$startupScriptPkgName.$latestPkgVer\lib\"

            # Update nuget.exe and credential provider exe from startup package path to the Startup-Script path
            $newNugetExePath = Join-Path $scriptPackageLibPath "Nuget.exe"
            if (Test-Path $newNugetExePath) {
                xcopy $newNugetExePath, $scriptPath /Y /C | Out-Null
            }

            $newCredManagerPath = Join-Path $scriptPackageLibPath "CredentialProvider.VSS.exe"
            if (Test-Path $newCredManagerPath) {
                xcopy $newCredManagerPath, $scriptPath /Y /C | Out-Null
            }

            $clientStartupPath = Join-Path $scriptPackageLibPath "AzureServiceDeployClient.ps1"
            if (Test-Path $clientStartupPath) {
                xcopy $clientStartupPath $scriptPath /Y /C | Out-Null

                Set-Content -Path $startupPkgVersionFile $latestPkgVer

                # Remove AzureServiceDeployClientStartup directory in %localappdata%
                Remove-Item $scriptPackagePath -Force -Recurse -Confirm:$false

                . "$scriptPath\AzureServiceDeployClient.ps1"

                return
            }
        }
    }
    catch
    {
        Write-Warning "Failed to update current script, continue to run the existing one"
    }

    if (Test-Path $scriptPackagePath)
    {
        Remove-Item $scriptPackagePath -Force -Recurse -Confirm:$false
    }

    LaunchCmdlet
}

function EnsureDependencyPresence
{
    if (!(Test-Path $nugetPath))
    {
        $appLocalNugetPath = Join-Path $azureServiceDeployClientPath "nuget.exe" 
        if (Test-Path $appLocalNugetPath)
        {
            xcopy $appLocalNugetPath, $scriptPath /Y /C | Out-Null
        }
        else {
            Write-Host "Required dependencies not found. Copy the latest Ev2 cmdlets and try again."
        }
    }

    $credManagerPath = Join-Path $scriptPath "CredentialProvider.VSS.exe"
    if (!(Test-Path $credManagerPath))
    {
        $appLocalCredMgrPath = Join-Path $azureServiceDeployClientPath "CredentialProvider.VSS.exe"
        if (Test-Path $appLocalCredMgrPath)
        {
            xcopy $appLocalCredMgrPath, $scriptPath /Y /C | Out-Null
        }
        else {
            Write-Host "Required dependencies not found. Copy the latest Ev2 cmdlets and try again."
        }
    }
}

function DownloadStartupScriptPackage($scriptPackagePath, $latestPkgVer)
{
    # Recreate AzureServiceDeployClientStartup directory before downloading the latest client startup package to that dir.
    if (Test-Path $scriptPackagePath)
    {
        Remove-Item -Path $scriptPackagePath -Force -Recurse -Confirm:$false
    }

    New-Item -ItemType Directory $scriptPackagePath | Out-Null
    & $nugetPath install $startupScriptPkgName -Prerelease -version $latestPkgVer -o $scriptPackagePath -ConfigFile "$azureServiceDeployClientPath\Nuget.config"
}

function write-header 
{
    param ([string]$s)
    $greeting = "`n*** $s ***`n"
    return $greeting
}

function InstallLatestVersion($targetPath, $lastestPkg, $latestPkgVer)
{
    if (!(Test-Path $targetPath))
    {
        New-Item -ItemType Directory $targetPath | Out-Null
    }

    $asdc = Join-Path $targetPath $lastestPkg 

    Write-Host "Fetching latest version $latestVStr of $sdkPackageName package"
    
    & $nugetPath install $sdkPackageName -Prerelease -version $latestVStr -o $targetPath -ConfigFile "$azureServiceDeployClientPath\Nuget.config"

    if ($latestPkgVer -le "6.0.3")
    {
        if (!(Test-Path "$targetPath\Microsoft.IdentityModel.Clients.ActiveDirectory.5.2.7"))
        {
            Remove-Item -Path "$targetPath\Microsoft.IdentityModel.Clients.ActiveDirectory*" -Force -Recurse -Confirm:$false
            & $nugetPath install "Microsoft.IdentityModel.Clients.ActiveDirectory" -version "5.2.7" -o $targetPath -ConfigFile "$azureServiceDeployClientPath\Nuget.config"
        }
        if (!(Test-Path "$targetPath\Microsoft.Identity.Client.4.39.0"))
        {
            Remove-Item -Path "$targetPath\Microsoft.Identity.Client*" -Force -Recurse -Confirm:$false
            & $nugetPath install "Microsoft.Identity.Client" -version "4.39.0" -o $targetPath -ConfigFile "$azureServiceDeployClientPath\Nuget.config"
        }
    }

    if ($latestPkgVer -le "6.0.5")
    {
        xcopy "$asdc\lib\*.*" $targetPath /Y /C | Out-Null
    }
    else
    {
        xcopy "$asdc\lib\netstandard2.0\*.*" $targetPath /Y /C | Out-Null
    }
    
    $manifest = "$targetPath\AzureServiceDeployClient.manifest"

    Get-Content $manifest | % {
        $parts = $_.Split(',');
        $path = (Get-ChildItem -Directory "$targetPath\$($parts[0]).*")[0].Name;
        xcopy "$targetPath\$path\$($parts[1])\*.*" $targetPath /Y /C | Out-Null
    }

    Get-ChildItem -Directory -Exclude CmdLets,Samples,Schema $targetPath | %{ Remove-Item $_ -Force -Recurse -Confirm:$false }
}

function SetupNugetConfigFile
{
    $config = '<?xml version="1.0" encoding="utf-8"?>' +
        '<configuration>' +
            '<packageSources>' +
                '<add key="ExpressV2" value="{0}" />' +
            '</packageSources>' + 
            '<activePackageSource>' +
                '<add key="ExpressV2" value="{0}" />' +
            '</activePackageSource>' +
        '</configuration>'
    $config -f $ev2NugetSource | Out-File "$azureServiceDeployClientPath\Nuget.config" -Encoding ascii
}

function GetLatestPackageVersion($packageName, $source)
{
    $configFilePath = "$azureServiceDeployClientPath\Nuget.config"

    $packages = & $nugetPath list $packageName -Prerelease -Source $source -ConfigFile $configFilePath
    if (!($packages) -or ($packages -contains "No packages found.")) {
        # if no package found in the mirror source then throw
        throw
    }
  
    $versions = @()
    $vStrs = @()
    # Parsing all version string to version oject and get the latest
    foreach ($p in $packages) {
        if ($p.Contains($packageName)) {
            $vStr = $p.Split(' ')[1]
            $vStrs = $vStrs + $vStr
            $v = new-object Version($vstr.Split('-')[0])
            $versions = $versions + $v
        }
    }
    $latestVersion = ($versions | Sort -Descending)[0].ToString()
    $latestVStr = $vStrs | ? { $_.Contains($latestVersion) }

    return $latestVStr
}

function LaunchCmdlet
{
    try
    {
        # Check if any previous version already installed
        $versionFile = Join-Path $azureServiceDeployClientPath "versions.txt"
        $InstalledVersions = $null
        $prevVersion = $null
        $found = $false
        $latestVstr = $null
        if (Test-Path $versionFile)
        {
            $InstalledVersions = Get-Content $versionFile
            if ($InstalledVersions)
            {
                if ($InstalledVersions.GetType().Name -ieq "String")
                {
                    $prevVersion = $InstalledVersions
                }
                else
                {
                    $prevVersion = $InstalledVersions[$InstalledVersions.Length - 1]
                }
            }
        }

        # Ensuring dependency presence of nuget.exe and Cred Provider for back-compat
        EnsureDependencyPresence

        Write-Host "Checking for latest version of Azure Service Deploy cmdlets"
        # Query latest version

        $latestVstr = GetLatestPackageVersion $sdkPackageName $ev2NugetSource
        $lastestPkg = "$sdkPackageName.$latestVStr"
        if ($InstalledVersions)
        {
            $found = $InstalledVersions | ? { $_ -eq $latestVStr }    
        }

        if (!$found)
        {
            if ($prevVersion)
            {
                # try to delete all older version except n-1
                Get-ChildItem -Directory -Exclude $prevVersion $azureServiceDeployClientPath | %{ Remove-Item $_ -Force -Recurse -Confirm:$false -ErrorAction SilentlyContinue | Out-Null }
            }

            InstallLatestVersion -targetPath "$azureServiceDeployClientPath\$latestVStr" -lastestPkg $lastestPkg -latestPkgVer $latestVStr
            Set-Content -Path $versionFile $prevVersion
            Add-Content -Path $versionFile $latestVstr
            $scriptPath = Join-Path $azureServiceDeployClientPath $latestVstr
        }
        else
        {
            $scriptPath = Join-Path $azureServiceDeployClientPath $prevVersion
            $latestVStr = $prevVersion
        }
    }
    catch
    {
        if ($latestVstr)
        {
            Remove-Item (Join-Path $azureServiceDeployClientPath $latestVstr) -Force -Recurse -Confirm:$false
        }

        if ($prevVersion)
        {
            $scriptPath = Join-Path $azureServiceDeployClientPath $prevVersion
            $latestVStr = $prevVersion
        }
        else
        {
            Write-Error "Cannot access Nuget source to install the cmdlets at this time. Cannot fall back to a previously installed version either since none was found."
            Write-Warning "Please check network and try again."
            return
        }

        Write-Warning "Not able to fetch latest version of Azure Service Deploy cmdlets package"
        Write-Warning "Will continue to start with currently installed version of cmdlets if present."
    }

  

    $modulesToImport = @("Microsoft.Azure.Deployment.Express.Cmdlet")
    $prevModulesToImport = @("Microsoft.Azure.Deployment.Express.Client")

    $nversion_above_6_0_3 = $false

    if (Test-Path(Join-Path $scriptPath "$modulesToImport.dll"))
    {
        foreach ($e in $modulesToImport) {
            Import-Module -global (Join-Path $scriptPath "$e.dll")
        }
        $nversion_above_6_0_3 = $true
        $cmdlets = $modulesToImport | %{ Get-Command -Module $_ } | %{$_.Name}
    }
    else
    {
        foreach ($e in $prevModulesToImport) {
            Import-Module -global (Join-Path $scriptPath "$e.dll")
            Write-Host (Join-Path $scriptPath "$e.dll")
        }
        $cmdlets = $prevModulesToImport | %{ Get-Command -Module $_ } | %{$_.Name}
    }

    try
    {
        if ($nversion_above_6_0_3)
        {
            $fileVersion = [System.Diagnostics.FileVersionInfo]::GetVersionInfo("$scriptPath\Microsoft.Azure.Deployment.Express.Cmdlet.dll").FileVersion
        }
        else
        {
            $fileVersion = [System.Diagnostics.FileVersionInfo]::GetVersionInfo("$scriptPath\Microsoft.Azure.Deployment.Express.Client.dll").FileVersion
        }

        $Host.UI.RawUI.WindowTitle += " $fileVersion ($latestVStr)"
    }
    catch
    {
        # ignore error when Core language is not allowed in SAW machine
    }
}

Write-Host "Importing Ev2 PowerShell module.."

$scriptPath = Split-Path -Parent $PSCommandPath
$nugetPath = Join-Path $scriptPath "nuget.exe"
$azureServiceDeployClientPath = Join-Path $env:LOCALAPPDATA "Microsoft\AzureServiceDeployClient"

if (!(Test-Path $azureServiceDeployClientPath))
{
    New-Item -ItemType Directory $azureServiceDeployClientPath | Out-Null
}

SetupNugetConfigFile

if ($skipScriptUpdate)
{
    LaunchCmdlet
}
else
{
    CheckScriptUpdate
}
