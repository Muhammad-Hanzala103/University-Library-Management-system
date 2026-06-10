# Release Security Notes — Ilm o Kutub System

This document outlines the security posture of the release build, what is safe for public distribution, and what must remain private.

---

## 1. What Is Safe for GitHub

The following files and directories are safe to push to a public or university GitHub repository:

* All `.cs` source files (including `DbSeeder.cs` — passwords are hashed before storage and exist only as seed constants).
* All `.xaml` and `.xaml.cs` view files.
* All `.csproj`, `.slnx`, `.props` project files.
* `appsettings.json` (contains no credentials; only runtime flags).
* All `.md` documentation files in the repository root (sanitized of plaintext passwords).
* `scripts/deployment_smoke_test.ps1` and `scripts/security_scan.ps1`.
* `.gitignore`.
* `AGENTS.md`.

---

## 2. What Must Remain Private

The following must **never** be pushed to a public repository:

| Item | Reason |
|------|--------|
| `KicsitLibrary.db` | Contains hashed passwords and potentially configured SMTP credentials |
| `KicsitLibrary.db-wal` / `KicsitLibrary.db-shm` | SQLite transaction logs |
| `DEMO CREDENTIALS PRIVATE TEMPLATE.md` | Contains template placeholders for demo passwords |
| Any `.pfx` or `.cer` certificate files | Code-signing private keys |
| `Backups/` folder contents | May contain full database snapshots |
| `Locks/` folder contents | Runtime mutex artifacts |
| `Temp/` folder contents | Transient operation files |

---

## 3. Default Demo Account Policy

### Seeded Accounts
Six demo accounts are created during first-run database initialization via `DbSeeder.cs`:
* `superadmin`, `admin`, `librarian`, `assistant`, `auditor`, `viewer`

### Security Properties
* Passwords are hashed using PBKDF2 before storage.
* Plaintext passwords exist only as string literals in `DbSeeder.cs` (compile-time only).
* No plaintext passwords appear in public documentation, README, or release notes.

### Deployment Requirement
> [!CAUTION]
> All default demo account passwords **must** be changed before any campus-wide or production deployment. The application does not enforce password change on first login (see Known Limitations below).

---

## 4. SMTP Configuration Policy

### Current State
* SMTP host, port, user, password, and sender settings are stored in the `SystemSettings` database table.
* SMTP password is stored as plaintext in the local SQLite database.
* SMTP password is **never** exposed in:
  * Activity logs (masked as `***`)
  * Settings exports (excluded entirely)
  * Backup sidecar metadata (excluded)
  * Restore staging metadata (excluded)
  * Lease/lock metadata (excluded)

### Deployment Requirement
* Configure SMTP settings through the **Settings Management** UI only.
* Do not commit SMTP credentials in any configuration file.
* For production environments, consider network-level SMTP relay that does not require stored credentials.

---

## 5. Backup and Restore Safety Policy

### Backup Files
* SQLite backup files are created using the native SQLite backup API.
* Each backup is verified with `PRAGMA integrity_check` and SHA-256 hash.
* Backup sidecar metadata files (`.metadata.json`) exclude sensitive settings.
* Backup files contain the full database including `SystemSettings` — treat backups with the same access control as the live database.

### Restore Safety
* Restore operations require explicit user confirmation (typing `RESTORE`).
* Staged restores are verified before application.
* Failed restores trigger automatic rollback to the pre-restore state.

---

## 6. Known Limitations

| Limitation | Risk Level | Mitigation |
|------------|-----------|------------|
| No forced password change on first login for seeded accounts | Medium | Document in deployment checklist; change passwords manually |
| SMTP password stored as plaintext in SQLite | Medium | Local file access required; plan DPAPI encryption in future |
| No multi-factor authentication | Low | Desktop application with physical access control |
| No session timeout mechanism | Low | Single-user desktop application |
| No audit log encryption | Low | Logs contain masked values; database file access required |

---

## 7. Future Hardening Work

* **DPAPI Encryption**: Encrypt SMTP password and other sensitive settings at rest using `System.Security.Cryptography.ProtectedData`.
* **Forced Password Change**: Add `MustChangePassword` flag to `User` entity and enforce change on first login for seeded accounts.
* **Session Timeout**: Implement idle-timeout logout for unattended workstations.
* **Certificate Pinning**: Pin the ClickOnce update server certificate for tamper-resistant updates.
* **Audit Log Integrity**: Add tamper-detection checksums to activity log entries.
