# 🤝 Contribution & Pull Request Workflow

This project follows a structured Git workflow to ensure clean history, consistent code quality, and reproducible builds.

---

## 🌿 Branching Strategy

We use a simplified GitFlow approach:

* `main` → stable / production-ready
* `develop` → integration branch for ongoing work
* feature branches → created from `develop`

---

## 🚀 Creating a Feature or Documentation Change

### 1. Sync your local repository

```bash
git checkout develop
git pull origin develop
```

---

### 2. Create a new branch

```bash
git checkout -b <branch-name>
```

Example:

```bash
git checkout -b feature/add-sqlite-exporter
```

---

## 🏷️ Branch Naming Convention

### ✨ Feature changes

```text
feature/add-sqlite-exporter
feature/implement-poi-import
feature/add-render-endpoint
```

---

### 📝 Documentation changes

```text
docs/update-readme
docs/add-architecture-diagram
docs/fix-installation-guide
```

---

### 🐛 Bug fixes

```text
fix/sqlite-exporter-nullref
fix/api-validation-error
```

---

### 🔧 Maintenance / CI / dependencies

```text
chore/update-dependencies
chore/ci-add-flyway-setup
chore/cleanup-unused-code
```

---

## 💾 Working on your branch

### Check current branch

```bash
git branch
```

---

### See changes

```bash
git status
git diff
```

---

### Stage changes

```bash
git add .
```

or more explicitly:

```bash
git add src/
git add README.md
```

---

### Commit changes

```bash
git commit -m "feat: add sqlite exporter implementation"
```

---

## 🔄 Keep your branch up to date

Before pushing or opening a PR:

```bash
git checkout develop
git pull origin develop

git checkout <branch-name>
git rebase develop
```

👉 Resolve conflicts if necessary, then continue:

```bash
git rebase --continue
```

---

## 📤 Push your branch

```bash
git push origin <branch-name>
```

If rebased:

```bash
git push --force-with-lease
```

---

## 🔁 Creating a Pull Request (PR)

1. Push your branch
2. Open a PR in GitHub
3. Base branch: `develop`
4. Fill out the PR template

---

## ✅ PR Guidelines

Before merging:

* [ ] Code builds successfully
* [ ] All tests pass
* [ ] CI pipeline is green
* [ ] Changes are reviewed (self-review is fine)
* [ ] No debug code or unnecessary files

---

## 🔀 Merging Strategy

We use:

👉 **Squash Merge**

Result:

* one clean commit per feature
* readable history
* no unnecessary merge commits

---

## 🧠 When to use which branch type

| Change Type          | Branch Prefix |
| -------------------- | ------------- |
| New feature          | `feature/`    |
| Documentation update | `docs/`       |
| Bug fix              | `fix/`        |
| CI / dependencies    | `chore/`      |

---

## ⚡ Small changes (exceptions)

For very small changes (e.g. typo fixes), you may:

```bash
git checkout develop
git pull
git commit -am "docs: fix typo in README"
git push
```

👉 However, using PRs is still recommended.

---

## 🧹 Clean up after merge

After your PR is merged:

```bash
git checkout develop
git pull
git branch -d <branch-name>
```

---

## 🎯 Goal

This workflow ensures:

* clean Git history
* reproducible builds
* transparent changes
* professional collaboration standards

---
