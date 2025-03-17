param (
    [Parameter(Mandatory = $true)]
    [string]$endpoint,
    [Parameter(Mandatory = $true)]
    [string]$startMessageText,
    [string]$userId = "userId",
    [string]$displayName = "User Display Name"
)

function New-Thread {
    param (
        [string]$endpoint,
        [string]$startMessageText,
        [string]$userId,
        [string]$displayName
    )

    # Create a new thread
    $response = Invoke-RestMethod -Uri "$endpoint/api/v1/threads" -Method Post -ContentType "application/json" -Body (@{
        startMessage = @{
            text = $startMessageText
            userId = $userId
            displayName = $displayName
        }
    } | ConvertTo-Json)

    $threadId = $response.id
    if ($null -eq $threadId) {
        Write-Output "Failed to create thread"
        exit
    }

    Write-Output "Get Thread Url: $endpoint/api/v1/threads/$threadId"
    $getThreadResponse = Invoke-WebRequest -Uri "$endpoint/api/v1/threads/$threadId" -Method Get

    if ($getThreadResponse.StatusCode -ne 200) {
        Write-Output "Failed to get threads: $($getThreadResponse.StatusCode) $($getThreadResponse.StatusDescription)"
        exit
    }

    return $threadId
}

$threadId = New-Thread -endpoint $endpoint -startMessageText $startMessageText -userId $userId -displayName $displayName

Write-Output "Thread created: $($threadId)"
Write-Output "User>>> $startMessageText"

$agentMessages = @()

function Get-LatestAgentMessage {
    param ()
    
    $newAgentMessagesFound = $false
    $response = Invoke-RestMethod -Uri "$endpoint/api/v1/threads/$threadId/messages" -Method Get -ContentType "application/json"
    foreach ($msg in $response.value) {
        if ($msg.role -eq "User") {
            continue
        }
        if ($msg.text -and $agentMessages -notcontains $msg.text) {
            $agentMessages += $msg.text
            Write-Output "Agent>>> $($msg.text)"
            $newAgentMessagesFound = $true
        }
    }

    return $newAgentMessagesFound
}

function New-Message {
    param (
        [string]$messageText,
        [string]$threadId
    )

    $response = Invoke-RestMethod -Uri "$endpoint/api/v1/threads/$threadId/messages" -Method Post -ContentType "application/json" -Body (@{
        text = $messageText
        userId = $userId
        displayName = $displayName
    } | ConvertTo-Json)

    if ($response.StatusCode -ne 201) {
        Write-Output "Failed to post message: $($response.StatusCode) $($response.StatusDescription)"
    }
}

while ($true) {
    Write-Output "Waiting for agent response..."
    $msgFound = Get-LatestAgentMessage 
    if (-not $msgFound) {
        Start-Sleep -Seconds 5
        continue
    }

    $msg = Read-Host "User>>> "
    if ($msg -eq 'exit') {
        Write-Output "Exiting chat..."
        break
    }
    New-Message -messageText $msg -threadId $threadId
}

