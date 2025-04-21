# Set API hostname (replace with your actual hostname)
$apiHostname = "localhost:7023"

# Configure to ignore SSL certificate validation
if ($PSVersionTable.PSVersion.Major -ge 6) {
    # For PowerShell Core (v6+)
    $skipCertParam = @{SkipCertificateCheck = $true}
} else {
    # For Windows PowerShell (v5.1 and below)
    $skipCertParam = @{}
    add-type @"
    using System.Net;
    using System.Security.Cryptography.X509Certificates;
    public class TrustAllCertsPolicy : ICertificatePolicy {
        public bool CheckValidationResult(
            ServicePoint srvPoint, X509Certificate certificate,
            WebRequest request, int certificateProblem) {
            return true;
        }
    }
"@
    [System.Net.ServicePointManager]::CertificatePolicy = New-Object TrustAllCertsPolicy
}

# Get all threads and extract IDs
try {
    $response = Invoke-RestMethod -Uri "https://$apiHostname/api/v1/threads" -Method GET -ContentType "application/json" @skipCertParam
    
    # Extract the thread IDs
    $threadIds = $response.value | ForEach-Object { $_.id }

    # Loop through and delete each thread
    foreach ($id in $threadIds) {
        Write-Host "Deleting thread $id..."
        Invoke-RestMethod -Uri "https://$apiHostname/api/v1/threads/$id" -Method DELETE @skipCertParam
        Write-Host ""
    }

    Write-Host "All threads deleted."
}
catch {
    Write-Error "An error occurred: $_"
}
