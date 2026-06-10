# GitHub Push & Repository Rename Checklist — Ilm o Kutub System

This checklist ensures that the repository is thoroughly audited, tested, and secure before pushing to the remote origin or renaming the repository.

---

## 1. Pre-Push Verification Checklist

- [ ] **Final Build**: Verified that `dotnet build KicsitLibrary.slnx` compiles with `0 errors` and `0 warnings` (excluding xUnit analyzer recommendations).
- [ ] **Final Tests**: Verified that `dotnet test KicsitLibrary.slnx` runs and passes all `302 integration tests` cleanly.
- [ ] **Smoke Test**: Verified that `powershell -ExecutionPolicy Bypass -File ./scripts/deployment_smoke_test.ps1` runs and reports Portable publish readiness with no database leakage.
- [ ] **Security Scan**: Verified that `powershell -ExecutionPolicy Bypass -File ./scripts/security_scan.ps1` passes all 11 Checks recursively.
- [ ] **Git Status Review**: Verify tracked changes using `git status`. Ensure only the expected files are staged.
- [ ] **No Private Credentials**: Double-check that no plaintext passwords exist in `DEMO CREDENTIALS PRIVATE TEMPLATE.md` or other files.
- [ ] **No Local Database**: Ensure SQLite files (`*.db`, `*.db-wal`, `*.db-shm`) are untracked and ignored.
- [ ] **No Build Artifacts**: Ensure `bin/`, `obj/`, `artifacts/`, and `publish/` directories are not committed.
- [ ] **No Private Paths**: Ensure no local workstation directories (e.g. `C:\Users\username`) are hardcoded in code or docs.

---

## 2. Git Remote & Branch Configuration

- [ ] **Target Remote**: `origin` -> `https://github.com/Muhammad-Hanzala103/University-Library-Management-system.git`
- [ ] **Active Branch**: `main`
- [ ] **Recommended Commit Message**: `Finalize Ilm o Kutub System release documentation and security hardening`
- [ ] **Push Approval**: Push command must not be run until explicit user confirmation/approval is given.

---

## 3. Recommended Push Procedure

Execute the following commands from the repository root:
```powershell
# 1. Stage all changes
git add -A

# 2. Commit changes
git commit -m "Finalize Ilm o Kutub System release documentation and security hardening"

# 3. Push to remote (Requires User explicit approval first)
git push origin main
```

---

## 4. Manual Repository Rename Checklist

The university library rename is completed inside the application UI and build metadata, but the repository name should be manually aligned:
- [ ] Log in to GitHub.
- [ ] Navigate to the repository settings for `University-Library-Management-system`.
- [ ] Rename the repository to: `Ilm-o-Kutub-System`.
- [ ] Update local Git origin configurations to point to the new URL if renamed:
  ```powershell
  git remote set-url origin https://github.com/Muhammad-Hanzala103/Ilm-o-Kutub-System.git
  ```

---

## 5. Release Tag Plan

After the commit is pushed successfully to `main`:
- [ ] Create a local tag:
  ```powershell
  git tag -a v1.0.0-demo -m "First-run university lab demo release"
  ```
- [ ] Push the tag to GitHub:
  ```powershell
  git push origin v1.0.0-demo
  ```
