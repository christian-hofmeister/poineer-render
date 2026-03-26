
# Git Workflow: After Merging a Pull Request

## 1. Switch to develop and update
```bash
git checkout develop
git pull
```

# Git Workflow: After Merging a Pull Request

## 2. Delete local featrue branch
```bash
git branch -d feature/your-branch-name
```
Use -d (safe delete).
If Git refuses, the branch was not fully merged.

## 3. Delete remote branch (if still exists)
```bash
git push origin --delete feature/your-branch-name
```
Only required if the branch was not deleted automatically after merge.

## 4. Clean up stale remote references
4. Clean up stale remote references
```bash
git fetch --prune
```
Removes local references to deleted remote branches.

## Summary
- Update develop
- Delete local branch
- Delete remote branch (if needed)
- Prune obsolete references


---
