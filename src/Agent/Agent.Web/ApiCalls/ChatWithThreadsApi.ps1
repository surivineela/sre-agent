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
        Write-Host "Failed to create thread"
        exit
    }

    Write-Host "Get Thread Url: $endpoint/api/v1/threads/$threadId"
    $getThreadResponse = Invoke-WebRequest -Uri "$endpoint/api/v1/threads/$threadId" -Method Get

    if ($getThreadResponse.StatusCode -ne 200) {
        Write-Host "Failed to get threads: $($getThreadResponse.StatusCode) $($getThreadResponse.StatusDescription)"
        exit
    }

    return $threadId
}

$threadId = New-Thread -endpoint $endpoint -startMessageText $startMessageText -userId $userId -displayName $displayName

Write-Host "Thread created: $($threadId)"
Write-Host "User>>> $startMessageText"

$agentMessages = @()

function Get-LatestAgentMessage {
    param (
        [string]$endpoint,
        [string]$threadId
    )
    
    $newAgentMessagesFound = $false
    $response = Invoke-RestMethod -Uri "$endpoint/api/v1/threads/$threadId/messages" -Method Get -ContentType "application/json"
    foreach ($msg in $response.value) {
        if ($msg.author.role -ne "SREAgent") {
            continue
        }
        if ($msg.text -and ($agentMessages -notcontains $msg.text)) {
            $agentMessages += $msg.text
            Write-Host "Agent>>> $($msg.text)"
            $newAgentMessagesFound = $true
        }
    }
    
    Write-Host "Agent messages found: $($newAgentMessagesFound)"
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
        Write-Host "Failed to post message: $($response.StatusCode) $($response.StatusDescription)"
    }
}

while ($true) {
    Write-Host "Waiting for agent response..."
    $msgFound = Get-LatestAgentMessage -endpoint $endpoint -threadId $threadId
    if (-not $msgFound) {
        Start-Sleep -Seconds 5
        continue
    }

    $msg = Read-Host "User>>> "
    if ($msg -eq 'exit') {
        Write-Host "Exiting chat..."
        break
    }
    New-Message -messageText $msg -threadId $threadId
}

