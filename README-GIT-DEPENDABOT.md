# Git Workflow: Handling Dependabot Branches

## 1. Update develop
```bash
git checkout develop
git pull
```
2. Create local working branch from dependabot
```bash
git checkout -b chore/update-dependency \
  origin/dependabot/nuget/<path-to-dependency-branch>
```
3. Rebase onto latest develop
```bash
git fetch origin
git rebase origin/develop
```
Resolves conflicts early and keeps history clean.

4. Run tests
```bash
dotnet test
```

Ensure everything still works after dependency update.

5. Merge into develop
```
git checkout develop
git merge chore/update-dependency
```

Usually results in a fast-forward merge if nothing changed in between.

6. Cleanup
```bash
git branch -d chore/update-dependency
git fetch --prune
```