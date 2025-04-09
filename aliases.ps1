function delete3p {
    $bashPath = findBash
    $command = "src/Agent/Infra/Scripts/delete.bash"
    & $bashPath -c "$command $args"
}

function deploy3p {
    $bashPath = findBash
    $command = "src/Agent/Infra/Scripts/deploy.bash"
    & $bashPath -c "$command $args"
}

function findBash {
    $gitBashPath = "C:\Program Files\Git\bin\bash.exe"
    if (Test-Path $gitBashPath) {
        return $gitBashPath
    } else {
        Write-Host "Git Bash not found at $gitBashPath. Falling back to default bash."
        return "bash"
    }
}
