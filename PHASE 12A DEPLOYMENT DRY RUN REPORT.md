# Phase 12A: Release Packaging & Deployment Dry Run Report

This report documents the results of the Phase 12A release packaging dry run, analyzes the publish artifacts, verifies runtime path and data preservation behaviors, outlines manual verification steps, and recommends a release packaging strategy for **Ilm-o-Kutub System**.

---

## 1. Dry Run Verification Results

### Build & Test Status
- **Clean & Restore**: Successful.
- **Build Compilation**: Succeeded with **0 warnings** and **0 errors**.
- **Automated Test Run**: **283 total tests passed** successfully (0 failed, 0 skipped, 0 warnings).

### Publish Command Used
```powershell
dotnet publish KicsitLibrary.Desktop/KicsitLibrary.Desktop.csproj -c Release -r win-x64 --self-contained false -o artifacts/deployment-smoke/publish
```

### Publish Folder Verification
- **Output Path**: `artifacts/deployment-smoke/publish/`
- **Main Executable**: `KicsitLibrary.Desktop.exe` (Verified Present)
- **Configuration**: `appsettings.json` (Verified Present)
- **Core Library DLLs**: (Verified Present)
  - `KicsitLibrary.Core.dll`
  - `KicsitLibrary.Data.dll`
  - `KicsitLibrary.Services.dll`
  - `KicsitLibrary.Reports.dll`
- **Database Presence Check**: No stray `KicsitLibrary.db` was copied into the publish directory (Verified Safe).

---

## 2. Runtime Behavior & Deployment Readiness

### Runtime Data Path Behavior
- **Development Mode**: By default, the application runs with `UseReleaseDataRoot=False` and `RuntimeStorageMode=Development`, keeping the database relative to the executable path (`KicsitLibrary.db`). This avoids silent target folder migrations and ensures compatibility with development/demo runs.
- **Release Mode**: When `UseReleaseDataRoot` is configured as `True` and `RuntimeStorageMode` is `Release`, data paths resolve safely to `%LOCALAPPDATA%\Ilm-o-Kutub System`.

### Backup and Restore
- **Backup**: Online SQLite backups (`Microsoft.Data.Sqlite.SqliteConnection.BackupDatabase`) are fully active. Backups generate unique timestamped filenames under `Documents\Ilm-o-Kutub Backups` by default (when custom path settings are empty).
- **Restore Staging**: When staging a restore, the application validates the SQLite database integrity, stages the copy, writes pending metadata, and instructs the user to restart. The pending metadata is applied safely at the next startup.

### Document Storage
- Uploaded administrative documents (Library SOPs, Rates, Policies, Audits) copy to `%USERPROFILE%\Documents\Ilm-o-Kutub System\Documents` by default under managed storage. No relative source tree pollution occurs.

### Database Ownership & Lock Files
- Cross-process and domain-specific database lock files (mutexes and `.lock` files) are created inside the dynamic lock directory to prevent concurrent access by multiple instances.

---

## 3. Releases/Packaging Strategy Comparison

The following table compares the four release packaging alternatives for the university deployment:

| Option | Ease of Demo | Update Support | Security & Sandboxing | Recommendation |
| :--- | :--- | :--- | :--- | :--- |
| **Portable Publish** | **Excellent** (Extract and run `KicsitLibrary.Desktop.exe`) | Manual replacement | None (runs in user space) | **Primary choice for University Demo** |
| **ClickOnce** | **Good** (Requires simple web page or folder launch) | **Automatic** (Silent background updates on launch) | Standard user permissions | **Primary choice for University Internal Deployment** |
| **MSIX Package** | Medium (Requires app signing and side-loading enablement) | Automatic via Windows Store / App Installer | Sandboxed | Deferred (Good for official Windows App Store release) |
| **MSI Installer (Wix/Setup)** | Good (Traditional wizard-based install) | Requires manual download and reinstall | Elevated admin permissions | Deferred (Traditional enterprise install) |

---

## 4. Phase 12A Recommendations

1. **For University Demo**: Use **Portable Publish** first. It is the fastest, lowest-risk deployment path. Librarians and professors can run the compiled executables directly from a USB drive or a shared network folder.
2. **For University Internal Deployment**: Adopt **ClickOnce** once automatic updating is requested, enabling seamless application updates.
3. **For Market-style Windows packaging**: Defer MSIX/MSI setups until general public availability.

---

## 5. Risks and Mitigations
- **Read-Only Directory Constraint**: If a portable deployment is placed in a read-only directory (e.g. `C:\Program Files`), SQLite will fail to write data unless `UseReleaseDataRoot=True` is enabled. 
  - *Mitigation*: Ensure settings seed `UseReleaseDataRoot=True` in production builds, forcing data to reside in the user's writable `%LOCALAPPDATA%`.
- **Database Lock Files**: Lock files must clean up gracefully during crash rollbacks.
  - *Mitigation*: The `IDatabaseOwnershipService` implements stale lock file lease expiration (defaulting to 120 minutes) which administrators can manually clear from the Backup UI.

---

## 6. Manual Verification Checklist

- [ ] Sign in as Admin and verify **Settings** UI configuration fields read/write correctly.
- [ ] Take a manual backup from the publish build output and verify its ZIP size and metadata.
- [ ] Stage a restore using a backup file, restart the application, and verify database changes.
- [ ] Confirm no lock-file conflict occurs when running the app from the published folder.
