# PowerShell script to extract docent from Jaunty repository
# Usage: .\extract-docent.ps1 -SourceRepo "C:\path\to\jaunty" -DestRepo "C:\path\to\docent-new"

param(
    [Parameter(Mandatory=$true)]
    [string]$SourceRepo,
    
    [Parameter(Mandatory=$true)]
    [string]$DestRepo
)

Write-Host "=== Docent Extraction Script ===" -ForegroundColor Cyan
Write-Host ""

# Validate source repo
if (-not (Test-Path "$SourceRepo\.git")) {
    Write-Host "Error: Source is not a git repository" -ForegroundColor Red
    exit 1
}

# Validate docent folder exists
if (-not (Test-Path "$SourceRepo\docent")) {
    Write-Host "Error: docent folder not found in source repo" -ForegroundColor Red
    exit 1
}

Write-Host "Source: $SourceRepo" -ForegroundColor Green
Write-Host "Destination: $DestRepo" -ForegroundColor Green
Write-Host ""

# Create destination directory
Write-Host "Creating destination directory..." -ForegroundColor Yellow
if (Test-Path $DestRepo) {
    Write-Host "Warning: Destination already exists. Removing..." -ForegroundColor Yellow
    Remove-Item -Recurse -Force $DestRepo
}
New-Item -ItemType Directory -Path $DestRepo | Out-Null

# Copy docent contents to root of destination
Write-Host "Copying docent files..." -ForegroundColor Yellow
Get-ChildItem "$SourceRepo\docent" -Exclude ".git" | Copy-Item -Destination $DestRepo -Recurse

# Initialize new git repo
Write-Host "Initializing new git repository..." -ForegroundColor Yellow
Push-Location $DestRepo
git init
git checkout -b main

# Get commit history for docent folder
Write-Host "Extracting commit history..." -ForegroundColor Yellow
Pop-Location

# Get unique commits that touched docent folder
$commits = git -C $SourceRepo log --all --oneline -- docent/ | ForEach-Object { $_.Split(' ')[0] }

if ($commits.Count -eq 0) {
    Write-Host "No commit history found for docent folder" -ForegroundColor Yellow
} else {
    Write-Host "Found $($commits.Count) commits affecting docent folder" -ForegroundColor Green
    
    # Create commits in new repo based on docent changes
    Push-Location $DestRepo
    
    # Get all files tracked
    git add .
    
    # Create initial commit with message from last docent commit
    $lastCommitMsg = git -C $SourceRepo log -1 --format="%s" -- docent/
    git commit -m "Initial commit: Docent documentation framework`n`nExtracted from Jaunty repository`n`n$lastCommitMsg"
    
    # Add remaining commits as a single commit with history reference
    if ($commits.Count -gt 1) {
        Write-Host "Creating history reference file..." -ForegroundColor Yellow
        $historyContent = "# Docent Git History (from Jaunty)`n`n"
        $historyContent += "Original commits affecting docent/ folder:`n`n"
        
        foreach ($commitHash in $commits) {
            $commitMsg = git -C $SourceRepo log -1 --format="%h | %s | %ai" $commitHash -- docent/
            $historyContent += "- $commitMsg`n"
        }
        
        $historyContent | Out-File -FilePath "HISTORY.md" -Encoding utf8
        git add HISTORY.md
        git commit -m "Add git history reference from Jaunty repository"
    }
    
    Pop-Location
}

Write-Host ""
Write-Host "=== Extraction Complete ===" -ForegroundColor Cyan
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "1. Review the extracted files in: $DestRepo"
Write-Host "2. Add remote: git -C '$DestRepo' remote add origin <your-docent-repo-url>"
Write-Host "3. Push: git -C '$DestRepo' push -u origin main"
Write-Host "4. Remove docent from Jaunty: git rm -r docent/ && git commit -m 'Remove docent (now standalone)'"
Write-Host ""
