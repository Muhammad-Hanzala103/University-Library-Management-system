# Source Control Cleanup Plan

Priority 9C status: planning only. No tracked build artifacts, databases, backups, documents, reports, certificates, or IDE files were removed from source control.

## 1. Current Finding

Priority 9B found that generated artifacts are already tracked in git, including some `bin`, `obj`, `.vs`, and local runtime database artifacts. Priority 9C keeps the same non-destructive rule: do not delete tracked files and do not rewrite history without explicit approval.

## 2. Current `.gitignore` Coverage

The repository now ignores future:

- `bin/` and `obj/`
- `artifacts/`, `publish/`, and `*.publish/`
- IDE user files and `.vs/`
- SQLite database files and sidecars
- backup, restore, document, report, certificate, log, and temporary artifacts
- generated CSV, XLSX, and PDF export files

This prevents most new runtime/build artifacts from being added accidentally, but it does not remove files already tracked.

## 3. Cleanup Must Be Explicit

When the owner approves cleanup, use non-destructive untracking commands only. Do not delete local working files that may contain data.

Recommended command pattern:

```powershell
git rm -r --cached -- bin obj .vs artifacts
git rm --cached -- '*.db' '*.db-wal' '*.db-shm' '*.sqlite' '*.sqlite-wal' '*.sqlite-shm'
git rm --cached -- '*.metadata.json' '*.zip' '*.csv' '*.xlsx' '*.pdf' '*.log' '*.tmp'
```

Review the staged list before committing:

```powershell
git status --short
git diff --cached --name-status
```

## 4. Files to Preserve Locally

Even after untracking, preserve local user/runtime data unless the owner explicitly requests deletion:

- `KicsitLibrary.db`
- SQLite `-wal` and `-shm` sidecars
- verified backup files
- pending restore metadata and staged/emergency restore files
- uploaded documents
- reports and certificates
- logs needed for support diagnosis

## 5. Cleanup Commit Scope

The cleanup commit should contain only:

- untracking generated artifacts
- `.gitignore` refinements if a missing pattern is discovered
- documentation note confirming no data was deleted

Do not combine source-control cleanup with installer packaging, EF migrations, database relocation, Supabase sync, or final README generation.

## 6. Verification After Cleanup

After the approved cleanup commit:

1. Run `dotnet build KicsitLibrary.slnx`.
2. Run `dotnet test KicsitLibrary.slnx`.
3. Run `./scripts/deployment_smoke_test.ps1`.
4. Confirm no ignored build/runtime artifacts appear in `git status --short`.
5. Confirm the development database still exists locally if it existed before cleanup.
