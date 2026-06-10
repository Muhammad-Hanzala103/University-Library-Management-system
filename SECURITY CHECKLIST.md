# Security Checklist — Ilm o Kutub System

This checklist must be reviewed and completed **before** any public GitHub push, university-wide deployment, or demo distribution.

---

## 1. Credential Review

- [ ] **Default Seeded Passwords**: Verify that `DbSeeder.cs` demo passwords are acceptable for the target environment.
- [ ] **Password Change Before Production**: All six default demo accounts (`superadmin`, `admin`, `librarian`, `assistant`, `auditor`, `viewer`) must have their passwords changed via the application before production use.
- [ ] **No Passwords in Public Docs**: Confirm that `README.md`, `RELEASE NOTES.md`, `INSTALLATION GUIDE.md`, and `DEMO CHECKLIST.md` do **not** expose any plaintext passwords.
- [ ] **Source Code Review**: Passwords exist only in `DbSeeder.cs` for initial seeding. They are hashed via PBKDF2 before storage.

---

## 2. SMTP Password Protection

- [ ] **Masking in UI**: SMTP password is displayed as `***` in settings views.
- [ ] **Masking in Exports**: SMTP password is excluded from settings export snapshots.
- [ ] **Masking in Activity Logs**: SMTP password changes are logged with masked values only.
- [ ] **Masking in Backup Metadata**: SMTP password is excluded from backup and restore sidecar metadata.
- [ ] **Storage Note**: SMTP password is stored as plaintext in the local SQLite `SystemSettings` table. Encrypted storage via Windows DPAPI is planned but deferred.

---

## 3. Settings Export Safety

- [ ] `ExportSettingsSnapshotAsync` excludes all keys in `SensitiveKeys` set (`SmtpPassword`, `SmtpUser`).
- [ ] Exported JSON files contain only non-sensitive settings.

---

## 4. Database Backup Safety

- [ ] Backup sidecar `.metadata.json` files do not include SMTP passwords or sensitive settings.
- [ ] SQLite backup files contain the full database (including SystemSettings), so they inherit the same access control requirements as the live database.
- [ ] Backups are stored under user-controlled directories (`Documents\Ilm-o-Kutub Backups`).

---

## 5. Database Restore Safety

- [ ] Restore staging metadata excludes SMTP passwords.
- [ ] Restore operations require explicit user confirmation (typing `RESTORE`).
- [ ] Restore candidate files are verified with `PRAGMA integrity_check` and SHA-256 before application.

---

## 6. Local Data Storage

- [ ] **Development Mode**: Database resolves relative to application base directory.
- [ ] **Release Mode**: Database relocates to `%LOCALAPPDATA%\Ilm-o-Kutub System\`.
- [ ] No database files (`.db`, `-wal`, `-shm`) are committed to source control.

---

## 7. GitHub Secret Scan

- [ ] Run `scripts/security_scan.ps1` before every push.
- [ ] Verify no high-risk patterns are flagged.
- [ ] Ensure `.gitignore` excludes database files, build artifacts, and temporary directories.

---

## 8. Repository Cleanup

- [ ] Build output (`bin/`, `obj/`) is not tracked.
- [ ] IDE cache (`.vs/`) is not tracked.
- [ ] Test results (`TestResults/`, `*.trx`) are not tracked.
- [ ] Database files (`*.db`, `*.db-wal`, `*.db-shm`) are not tracked.

---

## 9. Demo Account Handling

- [ ] Demo accounts are seeded automatically on first run for evaluation convenience.
- [ ] Demo passwords are documented only in `DbSeeder.cs` source code (not in public-facing documentation).
- [ ] Private demo handout (`DEMO CREDENTIALS PRIVATE TEMPLATE.md`) is available for instructor distribution.
- [ ] Demo accounts must have passwords changed before campus-wide deployment.

---

## 10. Pre-Release Approval Checklist

- [ ] `dotnet build` passes with 0 errors.
- [ ] All integration tests pass.
- [ ] `scripts/deployment_smoke_test.ps1` passes.
- [ ] `scripts/security_scan.ps1` passes (or only documented false positives remain).
- [ ] No plaintext passwords in any `.md` file (including `DEMO CREDENTIALS PRIVATE TEMPLATE.md` which has placeholder templates only).
- [ ] No private local machine paths in public documentation.
- [ ] No personal contact information exposed.
- [ ] SMTP settings are blank in the seeded database.
- [ ] Code-signing certificate is applied (for production ClickOnce only).
