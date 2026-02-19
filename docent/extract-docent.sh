#!/bin/bash
# Extract Docent from Jaunty repository
# Usage: ./extract-docent.sh /path/to/jaunty /path/to/docent-new

set -e

SOURCE_REPO="$1"
DEST_REPO="$2"

if [ -z "$SOURCE_REPO" ] || [ -z "$DEST_REPO" ]; then
    echo "Usage: $0 <source-repo> <dest-repo>"
    echo "Example: $0 /home/user/jaunty /home/user/docent"
    exit 1
fi

echo "=== Docent Extraction Script ==="
echo ""
echo "Source: $SOURCE_REPO"
echo "Destination: $DEST_REPO"
echo ""

# Validate source repo
if [ ! -d "$SOURCE_REPO/.git" ]; then
    echo "Error: Source is not a git repository"
    exit 1
fi

# Validate docent folder exists
if [ ! -d "$SOURCE_REPO/docent" ]; then
    echo "Error: docent folder not found in source repo"
    exit 1
fi

# Create destination directory
echo "Creating destination directory..."
rm -rf "$DEST_REPO"
mkdir -p "$DEST_REPO"

# Copy docent contents to root of destination
echo "Copying docent files..."
cp -r "$SOURCE_REPO/docent/"* "$DEST_REPO/"
cp "$SOURCE_REPO/docent/.gitignore" "$DEST_REPO/" 2>/dev/null || true

# Initialize new git repo
echo "Initializing new git repository..."
cd "$DEST_REPO"
git init
git checkout -b main

# Get commit history for docent folder
echo "Extracting commit history..."
cd "$SOURCE_REPO"
COMMITS=$(git log --all --oneline -- docent/ | wc -l)

if [ "$COMMITS" -eq 0 ]; then
    echo "No commit history found for docent folder"
else
    echo "Found $COMMITS commits affecting docent folder"
    
    cd "$DEST_REPO"
    
    # Add all files
    git add .
    
    # Get last commit message from original repo
    LAST_MSG=$(git -C "$SOURCE_REPO" log -1 --format="%s" -- docent/)
    
    # Create initial commit
    git commit -m "Initial commit: Docent documentation framework

Extracted from Jaunty repository

$LAST_MSG"
    
    # Create history file if multiple commits
    if [ "$COMMITS" -gt 1 ]; then
        echo "Creating history reference file..."
        echo "# Docent Git History (from Jaunty)" > HISTORY.md
        echo "" >> HISTORY.md
        echo "Original commits affecting docent/ folder:" >> HISTORY.md
        echo "" >> HISTORY.md
        git -C "$SOURCE_REPO" log --all --oneline -- docent/ | while read line; do
            echo "- $line" >> HISTORY.md
        done
        
        git add HISTORY.md
        git commit -m "Add git history reference from Jaunty repository"
    fi
fi

echo ""
echo "=== Extraction Complete ==="
echo ""
echo "Next steps:"
echo "1. Review the extracted files in: $DEST_REPO"
echo "2. Add remote: git -C '$DEST_REPO' remote add origin <your-docent-repo-url>"
echo "3. Push: git -C '$DEST_REPO' push -u origin main"
echo "4. Remove docent from Jaunty: git rm -r docent/ && git commit -m 'Remove docent (now standalone)'"
echo ""
