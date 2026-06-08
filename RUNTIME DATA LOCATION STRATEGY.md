# Runtime Data Location Strategy

Priority 9D status: release data-location planning plus guarded runtime path integration and verified database relocation workflow only. No installer, ClickOnce package, MSIX package, EF migration, database rename, or automatic startup relocation was performed.

## 1. Current Development Behavior

- SQLite connection string remains `Data Source=KicsitLibrary.db`.
- Startup still resolves relative SQLite paths from `AppContext.BaseDirectory`.
- With default settings, the database remains `KicsitLibrary.Desktop/bin/<Configuration>/net8.0-windows/KicsitLibrary.db`.
- Existing development databases are not moved, deleted, renamed, or recreated.

## 2. Runtime Path Settings

Priority 9C adds non-destructive `SystemSettings` defaults:

| Setting | Default | Purpose |
| --- | --- | --- |
| `RuntimeDataRoot` | empty | Optional explicit runtime data root. |
| `RuntimeStorageMode` | `Development` | Development or release storage mode. |
| `UseReleaseDataRoot` | `False` | Guard for release-safe runtime paths. |
| `DatabaseFileName` | `KicsitLibrary.db` | Stable SQLite file name. |
| `DocumentsFolderName` | `Documents` | Documents folder under runtime root. |
| `BackupsFolderName` | `Backups` | Backups folder under runtime root. |
| `ReportsFolderName` | `Reports` | Reports folder under runtime root. |
| `CertificatesFolderName` | `Certificates` | Certificates folder under runtime root. |
| `RestoreStagingFolderName` | `RestoreStaging` | Restore staging folder under runtime root. |
| `LogsFolderName` | `Logs` | Logs folder under runtime root. |
| `TempFolderName` | `Temp` | Temporary folder under runtime root. |
| `LocksFolderName` | `Locks` | Ownership lock folder under runtime root. |

## 3. Release-Safe Default

When release data root usage is explicitly enabled and `RuntimeDataRoot` is empty, `IRuntimePathService` resolves the runtime data root to:

```text
%LOCALAPPDATA%\Ilm-o-Kutub System
```

If `RuntimeDataRoot` is configured, that path is used as the root after normalization and path-traversal validation.

## 4. Guarded Database Path Decision

The database path is prepared for release relocation through `IRuntimePathService.GetDatabasePathAsync`, but startup is not switched to the runtime root by default.

Decision:

- Default development startup continues using the executable-relative SQLite path.
- Release database relocation requires `UseReleaseDataRoot=True` or `RuntimeStorageMode=Release` plus an explicit future startup integration task.
- This guard avoids silently creating a second empty database under AppData while the existing development database still lives beside the executable.

## 5. Integrated Runtime Defaults

Priority 9C integrates low-risk defaults only:

- `DocumentStorageService` uses `IRuntimePathService.GetDocumentStorageRootAsync()` when `DocumentStorageRoot` is empty.
- `BackupService` uses `IRuntimePathService.GetBackupRootAsync()` when `BackupDefaultFolder` is empty.
- `RestoreService` can use runtime restore staging only when the runtime database path matches the actual configured SQLite database path. Otherwise it preserves the existing beside-database staging path so startup can still apply pending restores.

## 6. Deferred Integrations

The following remain deferred to avoid breaking stable modules:

- Startup database relocation to runtime root.
- Report exporter default path integration.
- Clearance certificate default path integration.
- Ownership lock file relocation.
- Installer-specific per-user versus per-machine data policy.

## 7. Safety Rules

- Do not delete or rename `KicsitLibrary.db`.
- Do not migrate user data automatically.
- Do not move document, backup, report, certificate, restore, lock, or log folders without an approved migration plan.
- Do not store runtime data inside source-control folders for a release deployment.
- Back up the database before any release-location change.

## 8. Release Recommendation

For installer deployment, use a release runtime root under `%LOCALAPPDATA%\Ilm-o-Kutub System` unless the university approves a managed per-machine data location. Before enabling it, implement a tested database relocation/migration workflow that:

1. Detects an existing executable-relative database.
2. Creates a verified backup.
3. Copies or restores the database to the release root.
4. Preserves pending restore sidecars.
5. Verifies login and core data after relocation.
