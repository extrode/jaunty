# Extract Docent from Jaunty Repository

This script extracts the `docent/` folder with its git history into a standalone repository.

## Prerequisites

- Git installed
- PowerShell (Windows) or Bash (Mac/Linux)

## Instructions

### Option 1: Using Git Filter-Repo (Recommended)

```bash
# Install git-filter-repo if not installed
pip install git-filter-repo

# Clone Jaunty repo to a temp location
git clone <jaunty-repo-url> docent-temp
cd docent-temp

# Filter to only docent folder
git filter-repo --path docent/ --path-rename docent=:

# Add remote for new docent repo
git remote add origin <docent-repo-url>
git push -u origin main
```

### Option 2: Using Git Filter-Branch (Built-in)

```bash
# Clone Jaunty repo to a temp location
git clone <jaunty-repo-url> docent-temp
cd docent-temp

# Filter to only docent folder
git filter-branch --prune-empty --subdirectory-filter docent HEAD

# Add remote for new docent repo
git remote add origin <docent-repo-url>
git push -u origin main
```

### Option 3: Manual Extraction (PowerShell)

Run the accompanying `extract-docent.ps1` script:

```powershell
.\extract-docent.ps1 -SourceRepo "C:\path\to\jaunty" -DestRepo "C:\path\to\docent"
```

## After Extraction

1. Move the extracted `docent` folder outside Jaunty
2. Update any internal references
3. Set up CI/CD for the new repo
4. Update documentation links

## Cleanup (in Jaunty repo)

After confirming docent works standalone:

```bash
git rm -r docent/
git commit -m "Remove docent - now a standalone project"
git push
```
