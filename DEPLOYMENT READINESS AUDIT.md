# Deployment Readiness Audit

Priority 9B audit date: 2026-06-08  
Phase 12B audit date: 2026-06-10  
Product: Ilm-o-Kutub System  
Scope: deployment preparation, Phase 12A release packaging dry run, and Phase 12B ClickOnce installer, automatic update planning, and Splash Screen startup integration. No public installer, MSIX package, Supabase sync, EF migrations, WhatsApp delivery, final README, namespace rename, or database rename was created.



## 1. Current Project Structure

The solution uses six projects:

- `KicsitLibrary.Core`: domain entities, enums, interfaces, product branding constants, and shared models.
- `KicsitLibrary.Data`: EF Core `KicsitLibraryDbContext`, repositories, `DbSeeder`, and SQLite compatibility initializer.
- `KicsitLibrary.Services`: authentication, catalog, consumer, circulation, notifications, scheduler, reports integration, clearance, reservations, audit, inventory, backup, restore, ownership, and document services.
- `KicsitLibrary.Reports`: report contracts, providers, CSV exporter, Excel exporter, PDF exporter, and report file path resolution.
- `KicsitLibrary.Desktop`: .NET 8 WPF MVVM shell, views, ViewModels, theme resources, `App.xaml.cs`, and `appsettings.json`.
- `KicsitLibrary.Tests`: xUnit integration/regression tests using isolated temporary SQLite databases and folders.

## 2. Target Framework and Runtime

- Desktop app: `net8.0-windows`, `WinExe`, `UseWPF=true`.
- Library/test projects: `net8.0`.
- Runtime requirement for framework-dependent deployment: .NET 8 Desktop Runtime on Windows.
- Self-contained deployment can bundle the runtime, but output size will be larger.

## 3. Desktop App Entry Point

- WPF entry point: `KicsitLibrary.Desktop/App.xaml` and `App.xaml.cs`.
- `App.xaml.cs` builds the generic host, loads `appsettings.json` from `AppContext.BaseDirectory`, configures DI, initializes SQLite, applies compatibility SQL, seeds defaults, then opens `LoginWindow`.
- `ShutdownMode` remains `OnExplicitShutdown`.

## 4. Current Database Strategy

- Current strategy: `EnsureCreatedAsync` only.
- EF migrations are deliberately not implemented.
- Existing databases are evolved through conservative SQLite compatibility SQL for additive columns/tables/indexes only.
- Startup failure during database initialization is fatal and stops the app.

## 5. SQLite Database Location

- Configured connection string: `Data Source=KicsitLibrary.db`.
- Relative SQLite paths are resolved against `AppContext.BaseDirectory`.
- In normal Debug builds this becomes `KicsitLibrary.Desktop/bin/Debug/net8.0-windows/KicsitLibrary.db`.
- In published output this will become `<publish folder>\KicsitLibrary.db` unless the connection string is changed.
- This is acceptable for development and portable internal pilots, but needs a release decision before installer deployment because program folders may be read-only.
- Priority 9C adds `IRuntimePathService` and guarded runtime settings for release-safe paths. With defaults (`UseReleaseDataRoot=False`, `RuntimeStorageMode=Development`), startup behavior is unchanged.
- Priority 9D implements the verified release database relocation workflow. It uses a stable source snapshot, integrity validation, mandatory pre-relocation safety backup, SHA256 checksum comparison, and target rollback/restoration if verification fails. `UseReleaseDataRoot` is only enabled after successful verification.

## 6. Backup and Restore Behavior

- Manual backup uses the SQLite online backup API, not raw live-file copy.
- Backup default folder is empty in settings, which resolves to `%USERPROFILE%\Documents\Ilm-o-Kutub Backups`.
- Automatic backup is disabled by default and uses the same real backup service when enabled.
- Restore is staged and restart-required. The live database is not replaced while the app is running.
- Restore creates a safety backup and pending metadata beside the active SQLite database.
- Pending restore files and safety backups must be preserved during upgrades.

## 7. Document Storage Behavior

- `DocumentStorageRoot` defaults to empty.
- Empty root resolves through `IRuntimePathService`. In default development mode this preserves `%USERPROFILE%\Documents\Ilm-o-Kutub System\Documents`; in release-root mode it resolves under the runtime data root.
- Files are stored under document-type and year-month subfolders using generated names.
- Soft delete affects records only; physical deletion is deferred.
- Document storage must not be deleted during uninstall or upgrade unless the operator explicitly chooses data removal.

## 8. Report Export Folders

- Report exports default to `%USERPROFILE%\Documents\Ilm-o-Kutub Reports`.
- Clearance certificates default to `%USERPROFILE%\Documents\Ilm-o-Kutub Certificates`.
- Exported CSV, XLSX, and PDF files are runtime artifacts, not source files.

## 9. Settings Storage Behavior

- Build-time settings live in `KicsitLibrary.Desktop/appsettings.json`.
- Operational settings are seeded into and read from `SystemSettings`.
- SMTP, scheduler, backup, restore, ownership, and document settings are database-backed.
- Settings screen (Priority 10A) is completed for main administrative configurations.

## 10. Authentication and Default Admin Notes

- Seeded users include `superadmin`, `admin`, `librarian`, `assistant`, `auditor`, and `viewer`.
- Default seeded passwords are documented in code and must be changed before production use.
- Password hashing uses PBKDF2 through `IPasswordHasher`; do not change the hasher parameters without a migration/reset plan.

## 11. Current Test Count

Current expected automated result:

```text
Passed: 284
Failed: 0
Skipped: 0
```

Tests use temporary SQLite databases and do not use the real development database.

## 12. Build Commands

```powershell
dotnet build KicsitLibrary.slnx
```

Optional local publish smoke command:

```powershell
dotnet publish KicsitLibrary.Desktop/KicsitLibrary.Desktop.csproj -c Release -r win-x64 --self-contained false -o artifacts/deployment-smoke/publish
```

## 13. Test Commands

```powershell
dotnet test KicsitLibrary.slnx
```

Deployment smoke script:

```powershell
powershell -ExecutionPolicy Bypass ./scripts/deployment_smoke_test.ps1
```

## 14. Known Deployment Blockers

- No EF migration baseline or adoption plan exists.
- SQLite database path currently resolves to the executable folder by default. Runtime path support exists and the Priority 9D relocation workflow is implemented, but startup database relocation is still explicit and not automatic.
- No final installer, ClickOnce profile, MSIX manifest, signing certificate, or upgrade process exists.
- `.gitignore` was absent before Priority 9B; many generated artifacts are already tracked.
- Default seeded credentials must be changed for any real deployment.
- SMTP credentials are stored in local `SystemSettings` when configured; encrypted secret storage is not implemented.
- WPF UI automation is not implemented.

## 15. Required Pre Release Fixes

1. Decide the release database location: executable folder for portable/internal only, or per-user/per-machine app data for installer deployment.
2. Use the approved Priority 9D release database relocation workflow before enabling release-root startup.
3. Create an EF migration baseline/adoption plan or formally document continued compatibility-SQL support.
4. Remove tracked `bin`, `obj`, `.vs`, local database, and generated artifacts from source control in a separate approved cleanup.
5. Change default seeded passwords during first-run or require operator reset.
6. Add release signing metadata and certificate plan.
7. Add installer/update rollback policy with mandatory backup before upgrade.
8. Complete manual release test plan and record results.

## 16. Recommended Packaging Options

- Internal pilot: framework-dependent portable publish to a controlled folder, with .NET 8 Desktop Runtime installed.
- Internal university rollout: signed Windows Installer that installs binaries and preserves user data folders.
- Market-ready release: signed MSI/MSIX with versioned upgrades, rollback, support documentation, and migration strategy.

## 17. ClickOnce Suitability

ClickOnce is suitable for small internal deployments where automatic updates are useful and user-level installation is acceptable. It is less suitable while the SQLite database lives beside the executable because updates can replace application folders and complicate data preservation.

## 18. MSIX Suitability

MSIX is suitable for a market-ready, signed, modern Windows package if the app data/database paths are moved to appropriate app data locations. It is not the first choice until database and document storage migration behavior is finalized.

## 19. Portable Self Contained Publish Suitability

Portable self-contained publish is useful for lab/internal testing because it bundles .NET and avoids runtime install prerequisites. It is not a complete installer and does not manage shortcuts, upgrades, uninstall behavior, signing trust, or data migration.

## 20. Installer Recommendation

Recommended next packaging direction: a signed Windows Installer for university internal deployment after the database location and upgrade policy are settled. Keep ClickOnce as a secondary internal option and MSIX as a later commercial option.

## 21. Risk Matrix

| Risk | Impact | Likelihood | Mitigation |
| --- | --- | --- | --- |
| Database stored in install folder | High | Medium | Move or configure database under AppData for installer deployments. |
| Runtime path service not fully integrated | Medium | Medium | Complete startup, reports, certificates, locks, logs, and temp integration after database relocation is approved. |
| No migration baseline | High | High | Create migration adoption plan before production. |
| Tracked build/runtime artifacts | Medium | High | Use `.gitignore`; remove tracked artifacts only in approved cleanup. |
| Default seeded passwords | High | Medium | Force password change or document secure first-run reset. |
| SMTP secrets in SQLite settings | High | Medium | Add encrypted credential storage before production SMTP. |
| Missing WPF UI automation | Medium | High | Execute manual release plan and add automation later. |
| Restore pending files deleted by updater | High | Low | Preserve database folder and pending restore sidecars during updates. |
| Document storage moved unexpectedly | Medium | Medium | Document storage migration and backup policy. |

## 22. Final Release Checklist

- [ ] Build passes with zero warnings and errors.
- [ ] Tests pass with at least 284 tests.
- [x] Deployment smoke publish completes.
- [ ] Manual release test plan completed and signed off.
- [ ] Database location and upgrade behavior approved.
- [ ] Release data root and database relocation workflow approved and tested.
- [ ] Backup before update policy approved.
- [ ] Default credentials changed or forced reset implemented.
- [ ] SMTP secret handling approved.
- [ ] Signing certificate selected.
- [ ] Installer/update/rollback strategy approved.
- [ ] `.gitignore` cleanup committed; tracked artifacts cleanup handled separately.
- [ ] Final `README.md` generated only after release packaging is complete.
