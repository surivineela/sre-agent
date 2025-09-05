# Print all environment variables for troubleshooting
# Get-ChildItem -Path Env:

$CurrentPath = Get-Location
Write-Host "Current Path: $CurrentPath"

git --version

$BranchPrefix = "release/"
$SourceBranchRegex = "^refs/tags/\d+\.\d+\.\d+\.0$"

$Build_SourceBranch = $Env:BUILD_SOURCEBRANCH
$Git_CurrentTags = git tag --points-at HEAD
$Git_CurrentCommit = git rev-parse HEAD

Write-Host $LASTEXITCODE

Write-Host "The current build branch is $Build_SourceBranch"
Write-Host "The current build commit is $Git_CurrentCommit"
Write-Host "The GIT tags on HEAD are: $Git_CurrentTags"

if($Build_SourceBranch -notmatch $SourceBranchRegex) {
    Write-Error "The current build branch $Build_SourceBranch is not a valid main build tag. Please select a valid main build tag to create pipeline. Main build tags use the format X.Y.Z.0 where the last segment is always 0."
}

# if there are multiple tags, try to get the one that matches the requested source branch
if ($Git_CurrentTags -is [Array])
{
     $Git_CurrentTag = [System.Linq.Enumerable]::FirstOrDefault($Git_CurrentTags, [Func[object,bool]]{ param($tag), $Build_SourceBranch.Contains($tag)})
    Write-Host "Multiple tags found on commit. Using: $Git_CurrentTag"
}
else
{
    $Git_CurrentTag = $Git_CurrentTags
}

if($Build_SourceBranch -ne "refs/tags/$Git_CurrentTag") {
    Write-Error "The current tag $Git_CurrentTag doesn't match to the trigger branch of the pipeline $Build_SourceBranch. Please make sure you selected the right tag when running the pipeline."
}

$NewBranchName = "$($BranchPrefix)$($Git_CurrentTag)"

Write-Host "Checking if the branch $NewBranchName exists."

if (git ls-remote --exit-code --heads origin $NewBranchName) { 
    Write-Error "The branch $NewBranchName already exists."
}

Write-Host "Pushing new branch $NewBranchName"

git checkout -b $NewBranchName

$pipelineFile = '.\.pipelines\SREAgent-Runtime-Official.yaml'

$pipelineContent = Get-Content -Path $pipelineFile -Raw
$pipelineContent.Replace(
    '$(MAJOR).$(MINOR).$(BUILD).$(REVISION)', 
    $Git_CurrentTag.Substring(0, $Git_CurrentTag.LastIndexOf('.')) + '.$(Rev:r)'
    ) | Set-Content -Path $pipelineFile

git config --global user.email "srea-devs@microsoft.com"
git config --global user.name "SRE Agent Build Service"
git add .
git commit -m "Create new branch $NewBranchName"
git push --set-upstream origin $NewBranchName
git status
