# Packaging Strategy

Priority 9B status: planning only. Phase 12A status: release packaging dry run completed. Phase 12B status: ClickOnce deployment, automatic update strategy planned, and professional Splash Screen startup polish integrated. Portable publish dry run is verified. No public installer, MSIX package, or production public publish was created.

## 0. Current Packaging & Deploy Status
- **Portable Publish**: Completed and verified via dry run in Phase 12A. Output is located in `artifacts/deployment-smoke/publish`. Recommended primary choice for immediate university demo.
- **ClickOnce Package**: Planned and configured in Phase 12B. Recommended primary choice for internal university deployment due to seamless automatic update support.
- **Splash Screen**: Real WPF borderless SplashWindow added, displaying dynamic loading statuses and dynamic assembly version info during startup.
- **MSIX Package**: Suited for general market release; requires code-signing certificates and sideloading enablement. Currently deferred.
- **MSI Installer (WiX)**: Suitable for highly controlled offline university environments where IT administrators manage deployments. Currently deferred.
- **Supabase Cloud Sync**: Deferred because offline local operations are prioritized, and synchronization is out-of-scope for the current phase.
- **Final README**: Deferred to the final release phase to prevent stale documentation while internal modules undergo minor modifications.



## 1. Portable Publish Option

Portable publish copies the WPF app and dependencies to a folder. It is simple and useful for development, lab review, and internal pilot testing.

Example smoke command:

```powershell
powershell -ExecutionPolicy Bypass ./scripts/deployment_smoke_test.ps1
```

Pros:
- Fast to produce.
- Easy to inspect.
- No installer technology required.

Cons:
- No shortcuts, uninstall entry, signing trust, upgrade workflow, or data migration.
- Framework-dependent mode requires .NET 8 Desktop Runtime.
- The current relative database path will create/use `KicsitLibrary.db` inside the publish folder.
- Priority 9C prepares a runtime data root service, but portable publish still keeps the default executable-relative database until release-root startup is explicitly approved.

## 2. Self Contained Publish Option

Self-contained publish bundles the .NET runtime with the application.

Pros:
- Does not require preinstalled .NET Desktop Runtime.
- Good for controlled offline machines.

Cons:
- Larger output.
- Still not an installer.
- Runtime servicing requires republishing the app.

## 3. Framework Dependent Publish Option

Framework-dependent publish is smaller and relies on installed .NET 8 Desktop Runtime.

Pros:
- Smaller package.
- Runtime can be serviced centrally by Windows/admin updates.

Cons:
- Runtime prerequisite must be installed and verified.
- Support team must know which runtime version is required.

## 4. ClickOnce Option

ClickOnce can provide simple user-level deployment and automatic updates.

Suitability:
- Reasonable for internal university rollout if users install per-user and updates are centrally hosted.
- Risky until database/document paths are clearly separated from application update folders.

Requirements:
- ClickOnce profile.
- Signing certificate.
- Update location and versioning policy.
- Backup-before-update policy.

## 5. MSIX Option

MSIX is a modern Windows package model with strong signing and clean install/uninstall behavior.

Suitability:
- Better for a market-ready release after data paths are finalized.
- Not ideal yet because the app currently uses a relative SQLite database path beside the executable unless configured otherwise.

Requirements:
- MSIX manifest.
- Publisher identity.
- Signing certificate.
- App data migration behavior.

## 6. Windows Installer Option

A traditional MSI or installer package is the strongest fit for university-managed desktop machines.

Pros:
- Admin-controlled installation.
- Shortcuts, install location, repair/uninstall, and upgrade rules.
- Can include prerequisites or detect them.

Cons:
- Requires installer tooling and signing.
- Upgrade and rollback policies must be designed carefully.

## 7. Recommendation for University Internal Deployment

Recommended: signed Windows Installer after two pre-release fixes:

1. Decide whether `KicsitLibrary.db` remains beside the executable for a portable pilot or moves to a user/per-machine app data folder for installed deployments.
2. Define upgrade behavior that creates a verified backup before replacing application files.
3. If AppData storage is selected, implement and test the Priority 9D verified runtime-root database relocation workflow before packaging.

For immediate smoke testing only, use framework-dependent portable publish.

## 8. Recommendation for Market Ready Commercial Release

Recommended: signed MSIX or signed MSI after:

- EF migration baseline/adoption plan is complete.
- Database and document storage paths are release-safe.
- SMTP secret storage is encrypted.
- WPF UI automation or a signed manual test record exists.
- Final README, license, support policy, and release notes are complete.

## 9. Update Strategy

- Every update must run build and test verification first.
- Every update must create a verified backup before installing or replacing binaries.
- Updates must preserve:
  - `KicsitLibrary.db`
  - SQLite `-wal` and `-shm` files when the app is closed cleanly or recovered safely
  - pending restore metadata
  - restore safety backups
  - document storage
  - backup history files
  - reports and certificates

## 10. Signing Certificate Requirement

Any installer, ClickOnce package, or MSIX package should be signed. Unsigned packages will trigger avoidable trust warnings and are not suitable for broad deployment.

Certificate decision needed:
- Internal university certificate for campus-managed devices.
- Commercial code-signing certificate for public release.

## 11. Versioning Strategy

Priority 9B adds centralized metadata in `Directory.Build.props`:

- `Version`: `1.0.0`
- `AssemblyVersion`: `1.0.0.0`
- `FileVersion`: `1.0.0.0`
- `Product`: `Ilm-o-Kutub System`

Before packaging, define semantic versioning rules:
- Major: database or compatibility breaking changes.
- Minor: new module/release feature.
- Patch: fixes and documentation-only release updates.

## 12. Rollback Strategy

Rollback must preserve user data. Recommended policy:

1. Create a verified backup before update.
2. Install/update app binaries.
3. Launch and complete smoke verification.
4. If startup fails, restore previous app binaries and keep the pre-update database backup.
5. Never roll back the database automatically without operator confirmation.

## 13. Backup Before Update Policy

Before any update:

- Run manual verified backup.
- Record backup path and SHA-256.
- Confirm `PRAGMA integrity_check` passes.
- Keep the backup outside the install folder.
- Block update if backup creation or verification fails.

## 13A. Runtime Data Location Policy

Priority 9C adds `IRuntimePathService` and a release-safe path strategy. For installer packaging, the recommended data root is `%LOCALAPPDATA%\Ilm-o-Kutub System` unless the university approves a managed per-machine data folder.

Do not enable release-root startup until a future task verifies:

- existing executable-relative database detection
- verified backup before relocation
- safe copy/restore into the release root
- pending restore sidecar preservation
- document and backup folder continuity
- login and core module verification after relocation

## 14. Database Compatibility Warning

The current schema strategy uses `EnsureCreatedAsync` and additive SQLite compatibility SQL. This is not a production migration strategy. Installer deployment should not proceed broadly until a migration baseline/adoption policy is approved.

## 15. Document Storage Migration Warning

Document storage defaults to the user's Documents folder. If future releases move document storage, a migration plan must preserve stored files and update `DocumentUploads.StoredFilePath` safely. Do not delete physical document files during uninstall or upgrade by default.

## 16. Test Checklist Before Publishing

- [ ] `dotnet build KicsitLibrary.slnx`
- [ ] `dotnet test KicsitLibrary.slnx`
- [ ] `powershell -ExecutionPolicy Bypass ./scripts/deployment_smoke_test.ps1`
- [ ] Runtime path tests pass with `RuntimePathServiceTests`
- [ ] Fresh install simulation
- [ ] First-run database creation
- [ ] Login with seeded admin account, then password-change policy check
- [ ] Catalog, consumer, circulation, reservation, clearance, reports, backup, restore, automatic backup, ownership, documents, and audit logs manual checks
- [ ] Offline operation check
- [ ] Upgrade simulation with backup before update
- [ ] Failure recovery check
