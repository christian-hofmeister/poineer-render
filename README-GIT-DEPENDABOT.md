# Git Workflow: Handling Dependabot Branches

This document describes a practical workflow for reviewing, rebasing, testing, and merging Dependabot updates in a controlled and reproducible way.

The goal is to:

* keep `develop` up to date
* review dependency updates before merging
* resolve conflicts early
* ensure CI and local tests stay green
* clean up local and remote references afterwards

---

## Overview

Dependabot usually creates remote branches automatically, for example:

```text
origin/dependabot/nuget/tests/POIneer.Render.IntegrationTests/Microsoft.AspNetCore.TestHost-10.0.5
```

Instead of merging these branches directly without review, it is recommended to:

1. update `develop`
2. create a local working branch from the Dependabot branch
3. rebase the local branch onto the latest `develop`
4. run tests locally
5. push the working branch if needed
6. open or complete the Pull Request
7. clean up merged branches

---

## 1. Update `develop`

Before working on any Dependabot change, make sure your local `develop` branch is current.

```bash
git checkout develop
git pull origin develop
```

Optional verification:

```bash
git status
git branch
```

This ensures that the dependency update is rebased or merged against the latest integration branch and reduces the chance of unnecessary conflicts later.

---

## 2. Inspect available remote Dependabot branches

You can list all remote branches:

```bash
git branch -r
```

To narrow the output to Dependabot branches only:

```bash
git branch -r | grep dependabot
```

Example output:

```text
origin/dependabot/nuget/tests/POIneer.Render.IntegrationTests/Microsoft.AspNetCore.TestHost-10.0.5
origin/dependabot/nuget/dot-config/dotnet-reportgenerator-globaltool-5.5.4
```

This helps you identify the exact remote branch name.

---

## 3. Create a local working branch from the Dependabot branch

Create your own local branch from the remote Dependabot branch.

```bash
git checkout -b chore/update-testhost-10.0.5 \
  origin/dependabot/nuget/tests/POIneer.Render.IntegrationTests/Microsoft.AspNetCore.TestHost-10.0.5
```

### Why create your own branch?

Working on your own branch gives you full control:

* you can rebase safely
* you can amend commits if needed
* you can add fixes or CI adjustments
* you avoid working directly on the auto-generated Dependabot branch

### Naming suggestions

Use a short and meaningful `chore/` branch name, for example:

```text
chore/update-testhost-10.0.5
chore/update-reportgenerator-5.5.4
chore/update-junitxml-testlogger
chore/update-fluentassertions-8.9.0
```

Use `chore/` because dependency updates are usually maintenance changes, not new features.

---

## 4. Review what changed

Before rebasing or merging, inspect the dependency update.

Show the changed files:

```bash
git status
git diff --name-only origin/develop...HEAD
```

Show the actual diff:

```bash
git diff origin/develop...HEAD
```

Show the latest commit(s):

```bash
git log --oneline --decorate --graph -n 5
```

This is useful to confirm:

* which package was updated
* which project file changed
* whether only version numbers changed
* whether there are unexpected modifications

---

## 5. Rebase onto the latest `develop`

Fetch the latest state from the remote first:

```bash
git fetch origin
```

Then rebase your local working branch onto the latest `develop`:

```bash
git rebase origin/develop
```

### Why rebase?

Rebasing helps to:

* resolve conflicts early
* keep history linear
* ensure the dependency update is validated against the latest codebase

### If conflicts occur

Check the status:

```bash
git status
```

Resolve the conflicting files, then stage them:

```bash
git add <file>
```

Continue the rebase:

```bash
git rebase --continue
```

If necessary, abort the rebase:

```bash
git rebase --abort
```

---

## 6. Run tests locally

After rebasing, run the relevant tests locally.

For the full solution:

```bash
dotnet test
```

For a specific project:

```bash
dotnet test tests/POIneer.Render.IntegrationTests/POIneer.Render.IntegrationTests.csproj
```

For Release configuration:

```bash
dotnet test --configuration Release
```

If your pipeline also relies on formatting or build validation, you can additionally run:

```bash
dotnet build --configuration Release
dotnet format --verify-no-changes
```

This helps ensure the update does not break the build or existing behavior.

---

## 7. Push your working branch if needed

If you want to open a new PR or update an existing one from your own branch, push it to GitHub:

```bash
git push origin chore/update-testhost-10.0.5
```

If you rebased and already pushed before, use:

```bash
git push --force-with-lease
```

`--force-with-lease` is safer than `--force` because it protects against overwriting unexpected remote changes.

---

## 8. Create or complete the Pull Request

Open a Pull Request in GitHub with:

* **base branch**: `develop`
* **compare branch**: your local working branch pushed to origin

Recommended PR title examples:

```text
chore: update Microsoft.AspNetCore.TestHost to 10.0.5
chore: update dotnet-reportgenerator-globaltool to 5.5.4
chore: update FluentAssertions to 8.9.0
```

In the PR description, note:

* which package was updated
* whether conflicts were resolved
* which tests were run
* whether CI is green

---

## 9. Merge into `develop`

If everything is green and approved, merge the branch.

### Option A: Merge in GitHub

This is usually the preferred approach if branch protection and CI checks are active.

Typical choices:

* **Squash merge** for a single clean commit
* **Rebase merge** for linear history
* **Merge commit** if you explicitly want to preserve branch history

### Option B: Merge locally

If you merge locally, first switch back to `develop`:

```bash
git checkout develop
git pull origin develop
```

Then merge your working branch:

```bash
git merge chore/update-testhost-10.0.5
```

If no new changes were added to `develop` in the meantime, this will often be a fast-forward merge.

Finally, push the updated `develop` branch:

```bash
git push origin develop
```

---

## 10. Clean up local branches and stale remote references

After the update has been merged, delete your local working branch:

```bash
git branch -d chore/update-testhost-10.0.5
```

Prune stale remote references:

```bash
git fetch --prune
```

Optional:

```bash
git branch -r
```

This removes references to remote branches that no longer exist.

---

## 11. Deleting remote branches manually

Sometimes you may want to delete a remote branch manually.

Use:

```bash
git push origin --delete dependabot/nuget/tests/POIneer.Render.IntegrationTests/Microsoft.AspNetCore.TestHost-10.0.5
```

Important: use the branch name **without** the `origin/` prefix.

### Correct

```bash
git push origin --delete dependabot/nuget/tests/POIneer.Render.IntegrationTests/Microsoft.AspNetCore.TestHost-10.0.5
```

### Incorrect

```bash
git push origin --delete origin/dependabot/nuget/tests/POIneer.Render.IntegrationTests/Microsoft.AspNetCore.TestHost-10.0.5
```

The `origin/` prefix is only how Git displays remote-tracking branches locally.

---

## Recommended Workflow Summary

```bash
git checkout develop
git pull origin develop

git checkout -b chore/update-testhost-10.0.5 \
  origin/dependabot/nuget/tests/POIneer.Render.IntegrationTests/Microsoft.AspNetCore.TestHost-10.0.5

git fetch origin
git rebase origin/develop

dotnet test

git push origin chore/update-testhost-10.0.5
```

After merge:

```bash
git checkout develop
git pull origin develop
git branch -d chore/update-testhost-10.0.5
git fetch --prune
```

---

## Notes

* Prefer `chore/` for dependency updates
* Rebase before merging to catch conflicts early
* Run at least the relevant tests locally
* Prefer Pull Requests over direct pushes for traceability
* Clean up old local and remote references regularly

This keeps dependency maintenance clean, reviewable, and CI-friendly.
