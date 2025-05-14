# Note: This uses MSAL by default. ADAL is deprecated and uses binaries that may not be available on your system.
# Note: If you are not allowed access after logging in, try from your SAW.

Install-Module -Name MSAL.PS
# Install-Module -Name ADAL.PS

$authUrl = "https://login.microsoftonline.com/common"
$resourceId = "e416d988-e644-435e-8f13-69f1a005267f"
$clientId = "454f45e4-53dd-43c4-853b-8bf944c1c568"
$endpointUri = "https://resource-provider.genevaautomation.ms/"
$token = Get-MsalToken -Authority $authUrl -ClientId $clientId -RedirectUri $endpointUri -Scopes "$resourceId/.default"
#$token = Get-ADALToken -Authority $authUrl -Resource $resourceId -ClientId $clientId -RedirectUri $endpointUri -PromptBehavior "Auto"
$aadToken = $token.AccessToken
# Write-Output $token
# Write-Output "AAD Token: $token"
$headers = @{Authorization="Bearer $aadToken"}
$icmTokenExchangeUrl = "https://prod.microsofticm.com/sso2/token"
$icmToken = Invoke-RestMethod -Uri $icmTokenExchangeUrl -Method Post -Headers $headers -Body 'grant_type=aad_token'
return $icmToken.Access_Token
