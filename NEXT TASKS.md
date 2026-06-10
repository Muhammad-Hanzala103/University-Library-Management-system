# Next Development Priorities for Ilm-o-Kutub System

This document contains a structured task list outlining the implementation steps for the remaining modules.

---

## Priority 4A: Database Initialization & Test Infrastructure
- [x] Remove mixed `MigrateAsync` and `EnsureCreatedAsync` startup behavior.
- [x] Use `EnsureCreatedAsync` only for the current development database.
- [x] Make the SQLite database path predictable relative to the desktop executable.
- [x] Stop startup after fatal database initialization or seeding failures.
- [x] Configure xUnit and isolated SQLite integration tests.
- [x] Add circulation, catalog, seeding, notification-entity, activity-log, and overdue-calculation regression coverage.

## Priority 4B: Deterministic Overdue Query & Notification Records
- [x] Derive overdue items from active issue records and local calendar dates.
- [x] Add the `OverdueItem` projection and current fine calculation.
- [x] Implement `IOverdueService` and manual idempotent processing.
- [x] Implement `INotificationService` record creation, retry, read state, and cooldown checks.
- [x] Persist notification recipient snapshots, retry metadata, and deduplication keys.
- [x] Add non-destructive SQLite compatibility columns and indexes.
- [x] Add Overdue Reminders and Notifications MVVM views.
- [x] Wire navigation, DI, dashboard overdue counting, settings, and audit logs.
- [x] Add ten Priority 4B tests; all nineteen tests pass.

## Priority 4C: Manual Email Delivery
- **Goal**: Add explicit operator-triggered SMTP delivery for existing pending email records.
- [x] Define SMTP configuration without committing credentials.
- [x] Add an email delivery abstraction and bounded retry handling.
- [x] Update pending/failed/sent records with attempt timestamps and failure details.
- [x] Add integration tests with a fake email transport.
- [x] Add Notifications actions for selected, retry, batch, and settings validation.
- [x] Keep delivery manual; no background scheduler was added.

## Priority 4D: Background Overdue Scheduler
- **Goal**: Add a cancellation-aware hosted scanner only after manual delivery is verified.
- [x] Define scheduler interval, startup behavior, and disabled-by-default policies.
- [x] Create a DI scope for every scheduled or bulk manual scan.
- [x] Prevent overlapping scans and email batches within the application process.
- [x] Add bounded SQLite busy/locked retries and operational logging.
- [x] Add cancellation, status persistence, dependency failure, and duplicate-reminder tests.
- [x] Add scheduler status and run-now actions to Overdue Reminders.
- [x] Gate the hosted worker on successful database initialization.

Remaining deployment concern:
- [x] Add cross-process ownership protection before supporting multiple simultaneous desktop application instances. Completed in Priority 8D.

---

## Priority 5A: Reports & Export Foundation
- **Goal**: Add reusable data-first reporting and the first five production reports.
- [x] Define report contracts, data rows, filters, results, and export requests.
- [x] Implement Catalog, Issued Books, Overdue Books, Fine, and Notification providers.
- [x] Implement physical CSV, Excel, and PDF exports.
- [x] Add safe Documents-folder storage and non-overwriting file names.
- [x] Log export success and failure.
- [x] Add Reports dashboard, report-specific filters, preview, empty state, and export actions.
- [x] Wire Reports navigation and DI.
- [x] Add thirteen isolated provider/export tests; all fifty-two tests pass.

## Priority 5B: Extended Reports & Print Refinement
- **Goal**: Add the approved advanced operational reports and improve export quality.
- [x] Add Student Clearance, Student Borrowing History, and Faculty Staff Borrowing History reports.
- [x] Add Reservation, Lost And Damaged Books, and Deleted Books Archive reports.
- [x] Add Visit Detail and Audit reports.
- [x] Add Inventory, New Arrivals, and Stock Verification reports.
- [x] Group and search all sixteen reports in the Reports dashboard.
- [x] Validate date and number ranges through contract-driven filter inputs.
- [x] Improve PDF page metadata, repeating headers, wide-table handling, page numbers, and summaries.
- [x] Improve Excel metadata, filters, frozen headers, sizing, and typed formatting.
- [x] Preserve safe CSV escaping and deterministic date formatting.
- [x] Add sixteen isolated tests; all sixty-eight tests pass.

Deferred refinements:
- [ ] Add embedded Unicode PDF fonts and configurable logos/branding.
- [ ] Add explicit user-selected output paths through a dialog service.
- [ ] Add large-dataset paging or streaming after profiling.

---

## Priority 6A: Student & Faculty Clearance Workflow
- **Goal**: Provide audited, certificate-producing library clearance for students and faculty/staff.
- [x] Add clearance result, blocking-item, certificate, action, and history models.
- [x] Implement student and faculty/staff clearance checks.
- [x] Block active issues, unpaid/partial fines, and unresolved lost/damaged cases.
- [x] Add transaction-safe approval and revocation with required remarks/reasons.
- [x] Persist status, date, remarks, and approving user for both member types.
- [x] Add non-destructive SQLite compatibility columns and indexes.
- [x] Generate and log real PDF clearance certificates.
- [x] Add Clearance dashboard, worklists, filters, details, and borrowing/history dialog.
- [x] Update Student Clearance reporting with loss/damage blockers.
- [x] Add fourteen isolated tests; all eighty-two tests pass.

## Priority 6B: Reservation Lifecycle Completion
- **Goal**: Complete reservation queue, availability, expiry, cancellation, and fulfillment behavior.
- [x] Define reservation state-transition rules and authenticated action boundaries.
- [x] Validate active status, clearance status, duplicate holds, same-title active issues, and pending fines.
- [x] Implement deterministic queue ordering and configurable expiry processing.
- [x] Implement cancellation, availability, and transaction-safe fulfillment through circulation validation.
- [x] Create deduplicated in-app and email notification records when the first queue item becomes available.

## Priority 9E: Source Control Cleanup Execution
- **Goal**: Untrack generated artifacts from git while preserving all local files and maintaining build/test functionality.
- [x] Pre-cleanup audit: identified 1082 tracked artifacts (14 .vs/ IDE cache files, 407 bin/ build output files, 661 obj/ build intermediate files).
- [x] Verified .gitignore patterns comprehensive for all artifact types.
- [x] Updated .gitignore with missing patterns: TestResults/, *.trx, *.coverage, *.coveragexml, *.nupkg, Temp/, Locks/.
- [x] Executed `git rm -r --cached` for .vs/, all project bin/ directories, and all project obj/ directories.
- [x] Committed cleanup: 2 commits total (artifact untracking + .gitignore update).
- [x] Verified no source files (.cs, .xaml, .csproj, .slnx, .md, .json, .ps1) were untracked.
- [x] Verified all local files preserved on disk (database, backups, documents, reports, certificates, logs).
- [x] Post-cleanup build verification: 0 warnings, 0 errors, 33.20s elapsed.
- [x] Post-cleanup test verification: all 243 tests passed, 0 failed, 0 skipped.
- [x] Updated CURRENT STATUS.md, KNOWN ISSUES.md, CODEX CONTINUATION AUDIT.md, TEST COMMANDS.md, SOURCE CONTROL CLEANUP PLAN.md with Priority 9E completion details.
- [x] Documented that .gitignore is now active for future generated artifacts; existing tracked artifacts have been removed from version control only.
- [x] Integrate normal returns with first-queue availability without automatic issue or automatic email sending.
- [x] Add Reservation Management, Create Reservation, and Reservation Queue MVVM screens.
- [x] Update Reservation Report queue positions and lifecycle summaries.
- [x] Add eighteen isolated SQLite tests; all one hundred tests pass.
- [x] Keep Priority 6A clearance rules and existing circulation workflows backward compatible.

Deferred refinements:
- [ ] Add a configurable maximum active-reservation limit if the library approves a policy value.
- [ ] Add scheduled reservation expiry processing only as a separately approved future task.
- [ ] Add WPF UI automation and multi-process reservation concurrency testing before multi-client deployment.

---

## Priority 7A: Activity Log Browser & Audit Records Workflow
- **Goal**: Provide traceable compliance browsing and a complete audit-record lifecycle.
- [x] Add latest-500 activity log browsing with search, action, entity, user, and date filters.
- [x] Add activity details, summary values, and distinct filter options.
- [x] Export the current activity view through existing CSV, Excel, and PDF exporters.
- [x] Protect old-log soft deletion for Super Admin/Admin and archive the deleted-range summary.
- [x] Add audit list, details, action, status-summary, and attachment projection models.
- [x] Implement transaction-safe audit create, update, status change, and soft deletion.
- [x] Validate unique audit number and required audit date, type, and status.
- [x] Require remarks for status changes and reasons for deletion.
- [x] Write activity logs for every audit mutation.
- [x] Add Activity Logs and Audit Records MVVM screens, dialogs, navigation, and DI.
- [x] Reuse the existing Audit Report and export pipeline.
- [x] Add sixteen isolated SQLite tests; all one hundred sixteen tests pass.

Deferred refinements:
- [ ] Add soft-delete metadata to audit attachments only through an approved additive compatibility change.
- [ ] Add UI automation and cross-process audit mutation contention tests before multi-client deployment.

## Priority 7B: Inventory & Stock Verification Workflow
- **Goal**: Complete physical inventory reconciliation and stock-verification operations.
- [x] Add inventory models, authorization-aware CRUD, quantity adjustments, damage/repair actions, soft delete/restore, summaries, and activity logs.
- [x] Add persisted stock-verification sessions and entries through additive SQLite compatibility tables.
- [x] Add verification, mismatch remarks, bulk missing marking, completion summaries, and explicit-only reconciliation.
- [x] Create real Inventory and Stock Verification MVVM views, dialogs, navigation, and DI registrations.
- [x] Update Inventory and Stock Verification reports without changing the sixteen-report foundation.
- [x] Add nineteen isolated SQLite tests; all one hundred thirty-five tests pass.

Deferred refinements:
- [ ] Implement safe inventory document upload/removal only as a separately approved workflow with signature validation and application-data storage.
- [ ] Add WPF UI automation and multi-process inventory contention testing.

---

## Priority 8A: Verified Local SQLite Backup Creation
- **Goal**: Create safe, operator-triggered, verified backups without stopping normal SQLite use.
- [x] Use the Microsoft.Data.Sqlite online backup API instead of copying the live database file.
- [x] Add timestamped, sanitized, non-overwriting backup file creation.
- [x] Add read-only `PRAGMA integrity_check` verification and SHA-256 checksums.
- [x] Add metadata JSON, optional ZIP compression, backup history, summaries, filters, and activity logs.
- [x] Add additive `BackupHistories` compatibility table and indexes without migrations.
- [x] Add Backup and details MVVM screens, navigation, authorization, and DI.
- [x] Seed manual-backup settings and backup view/manage permissions.
- [x] Add thirteen isolated SQLite tests; all one hundred forty-eight tests pass.

Deferred refinements:
- [x] Add automatic retention only with a separately approved scheduler and explicit file-safety policy. Completed in Priority 8C.
- [ ] Add a native folder picker; Priority 8A supports the default Documents path and typed custom paths.
- [x] Add cross-process backup coordination protection. Completed in Priority 8D.
- [ ] Add WPF UI automation for backup coordination screens.

## Priority 8B: Verified Local SQLite Restore
- **Goal**: Restore only from validated local SQLite backups without replacing an active database.
- [x] Add restore request, preview, validation, result, history, and summary contracts.
- [x] Add additive restore-history persistence and indexes without migrations.
- [x] Require Admin/Super Admin authorization, reason, exact `RESTORE` confirmation, and a mandatory verified safety backup.
- [x] Validate source files with size checks, `PRAGMA integrity_check`, SHA-256, and required application schema tables.
- [x] Stage a verified copy and require application restart instead of replacing the active database.
- [x] Apply pending restores before host/database startup with an emergency copy, post-restore verification, and rollback.
- [x] Import startup results into restore history and activity logs.
- [x] Add Restore, preview/confirmation, history details, and Backup integration.
- [x] Add nineteen isolated restore tests; all one hundred sixty-seven tests pass.

Deferred refinements:
- [ ] Add WPF UI automation for restore confirmation and restart messaging.
- [x] Add cross-process ownership protection before supporting simultaneous desktop instances. Completed in Priority 8D.
- [ ] Add a native database-file picker; current restore paths are selected from backup history or typed manually.
- [ ] Define an operator-reviewed retention policy for staged, emergency, and safety backup files.

## Product Branding and Management UI Refinement
- [x] Rename all current user-visible product branding to **Ilm-o-Kutub System**.
- [x] Keep the institution identity separate in reports and certificates.
- [x] Preserve internal `KicsitLibrary.*` projects, namespaces, assembly name, solution name, and `KicsitLibrary.db` for compatibility.
- [x] Centralize product and artifact-folder names in `ProductBrand`.
- [x] Apply the professional management palette through shared colors and styles.
- [x] Polish sidebar labels without removing navigation items.
- [x] Add the global **Show Helpful Hints** session toggle.
- [x] Add practical tooltips to navigation, circulation, backup/restore, reports, clearance, reservations, audits, inventory, notifications, and scheduler actions.
- [x] Add four branding/hint regression tests; the full suite now has 188 passing tests after Priority 8C.
- [x] Keep deployment, Supabase sync, EF migrations, WhatsApp delivery, and final README work out of this phase.

### GitHub Repository Rename Checklist
- [ ] Go to the GitHub repository.
- [ ] Open **Settings**.
- [ ] Rename the repository to `Ilm-o-Kutub-System`.
- [ ] Update the local remote URL after rename if needed.
- [ ] Verify push and pull still work.
- [ ] Generate the final `README.md` during final release.

Safe CLI command for documentation only; do not run without authenticated access and explicit confirmation:

```powershell
gh repo rename Ilm-o-Kutub-System --repo OWNER/CURRENT_REPOSITORY
```

## Priority 8C: Automatic Backup Scheduling & Retention Safety Policy
- **Goal**: Add disabled-by-default automatic local SQLite backups and a conservative, logged retention policy.
- [x] Seed non-destructive automatic backup settings with scheduler, retention, physical deletion, and status defaults all safe.
- [x] Add scheduler contracts, result/status models, background service, startup signal, and scoped-service run pattern.
- [x] Ensure every automatic backup calls the existing real `IBackupService` online SQLite backup, verification, compression, history, and logging behavior.
- [x] Prevent overlapping manual/scheduled automatic backup runs with one scheduler semaphore.
- [x] Skip automatic backup while pending restore metadata exists and record a clear status/log entry.
- [x] Add retention preview and apply services with soft-delete history cleanup by default.
- [x] Protect the live database, latest successful backup, failed-verification backups, restore safety backups, emergency restore files, pending restore files, unsupported extensions, files outside the configured backup folder, and detectable symbolic/reparse paths.
- [x] Add Backup Management scheduler settings, status, run-now, save, preview retention, apply retention, open folder, physical deletion warning, and candidate grid.
- [x] Enforce Admin/Super Admin scheduler configuration/run/retention authorization, with explicit permission support for future role customization.
- [x] Add seventeen isolated SQLite scheduler/retention tests; all 188 tests pass.

Deferred refinements:
- [ ] Add WPF UI automation for automatic backup settings and retention confirmation.
- [x] Add cross-process backup/restore/scheduler ownership protection before supporting simultaneous desktop instances. Completed in Priority 8D.
- [ ] Add a native folder picker for backup destinations.

## Priority 8D: Cross Process Database and Backup Ownership Protection
- **Goal**: Coordinate app instances and critical database/backup operations through safe ownership locks.
- [x] Read `CriticalOperationLockTimeoutSeconds` and `LockFileRetentionMinutes` from `SystemSettings` with safe defaults of 15 seconds and 120 minutes.
- [x] Write non-sensitive lease metadata with operation, process, machine, user, acquired/expiry, lock name, and file path.
- [x] Add separate lock domains for application instance, database, backup folder, restore, and scheduler health.
- [x] Protect backup creation, restore staging, pending restore startup application, automatic scheduler runs, retention physical deletion, and compatibility initialization where safe.
- [x] Make lock release idempotent and ensure `RunWithCriticalOperationLockAsync` releases in `finally`.
- [x] Make stale cleanup skip active locks and delete only expired safe lock files.
- [x] Log cleanup cleaned/skipped/failed outcomes and unauthorized cleanup attempts.
- [x] Add Backup Management ownership status and stale cleanup actions.
- [x] Add fifteen real isolated SQLite ownership tests; all 203 tests pass.

Deferred refinements:
- [ ] Add WPF UI automation for ownership status and cleanup buttons.
- [ ] Add an operator-facing Settings screen for ownership settings instead of database-only `SystemSettings` editing.

## Priority 9A: Secure Document Upload Workflow
- **Goal**: Add a safe local document upload and management workflow for administrative library documents.
- [x] Add document upload request/result/list/details/validation/download/delete/storage/summary models.
- [x] Add `IDocumentService` and `DocumentService` with validation, upload, listing, details, open, copy, soft delete, restore, summaries, and related-entity lookup.
- [x] Add `IDocumentStorageService` and `DocumentStorageService` with configurable root, default Documents-folder storage, path normalization, generated names, non-overwrite behavior, and reparse-point checks.
- [x] Support Library SOP, National Library Rates, Library Policy, Audit Evidence, Visit Evidence, Invoice, Inventory Document, and General Document.
- [x] Reject blocked executable/script extensions and validate allowed extensions plus practical file signatures.
- [x] Persist original filename, generated stored name/path, size, SHA-256, content type, uploader, version, expiry, remarks, and related entity metadata.
- [x] Add non-destructive SQLite compatibility for `DocumentUploads` additive fields, safe table creation if missing, and document indexes.
- [x] Seed document settings and document permissions without destructive changes.
- [x] Add Documents navigation, management grid, filters, upload window, details window, open/copy/delete/restore actions, and DI registrations.
- [x] Add SOP Documents and National Library Rates Documents report providers only.
- [x] Add seventeen isolated SQLite/temp-folder tests; all 220 tests pass.
- [x] Keep deployment, Supabase sync, EF migrations, WhatsApp delivery, final README, namespace/database rename, and production database access out of this task.

Deferred refinements:
- [ ] Add WPF UI automation for document upload, details, filtering, copy, delete, and restore screens.
- [ ] Add inline related-document panels inside Audit, Visit, and Inventory details after manual UI validation.
- [ ] Add a native folder picker for document copy destinations; current WPF flow uses a save dialog to choose the destination folder.
- [ ] Add a future reviewed physical-file cleanup task. Priority 9A soft-deletes records only.
- [ ] Add an operator-facing Settings screen for document storage limits and allowed extensions.

## Priority 9B: Deployment Preparation Audit and Release Readiness Plan
- **Goal**: Complete a deployment preparation audit and release readiness plan before actual packaging.
- [x] Create `DEPLOYMENT READINESS AUDIT.md`.
- [x] Audit project structure, target framework, runtime, entry point, database strategy, SQLite location, backup/restore, document storage, report folders, settings, authentication notes, test count, commands, blockers, packaging options, risks, and final checklist.
- [x] Create `PACKAGING STRATEGY.md`.
- [x] Compare portable, self-contained, framework-dependent, ClickOnce, MSIX, and Windows Installer options.
- [x] Document the recommended university internal deployment path and market-ready release path.
- [x] Create `RELEASE TEST PLAN.md`.
- [x] Add `scripts/deployment_smoke_test.ps1` for build, test, and local publish smoke verification.
- [x] Add `.gitignore` for future build outputs, local databases, publish artifacts, backups, documents, reports, certificates, logs, and IDE files.
- [x] Add centralized release metadata through `Directory.Build.props` without renaming projects, namespaces, assemblies, or `KicsitLibrary.db`.
- [x] Confirm packaging is still pending, final `README.md` is still pending, and GitHub repository rename remains manual.

Deferred refinements:
- [ ] Remove already tracked `bin`, `obj`, `.vs`, local database, and generated runtime artifacts from source control only after explicit approval.
- [ ] Decide release database location before installer packaging.
- [ ] Create an EF migration baseline/adoption plan before production deployment.
- [ ] Add signing certificate and installer/update/rollback policy.
- [ ] Execute the release test plan manually before publishing.

## Priority 9C: Release Data Location and Source Control Cleanup Plan
- **Goal**: Prepare release-safe runtime data locations and a safe source-control cleanup plan without relocating current databases or removing tracked artifacts.
- [x] Create `RUNTIME DATA LOCATION STRATEGY.md`.
- [x] Create `SOURCE CONTROL CLEANUP PLAN.md`.
- [x] Add `IRuntimePathService` and `RuntimePathService`.
- [x] Seed runtime path settings non-destructively in `SystemSettings`.
- [x] Keep default development database behavior executable-relative with `UseReleaseDataRoot=False`.
- [x] Use runtime path defaults for document storage and backup only when explicit folders are empty.
- [x] Keep restore pending sidecars startup-compatible; use runtime staging only when the runtime database path matches the active configured database.
- [x] Update the deployment smoke script to report runtime data mode and non-destructive limitations.
- [x] Add eight isolated runtime path tests.
- [x] Do not create installers, ClickOnce, MSIX, EF migrations, Supabase sync, WhatsApp delivery, final README, namespace rename, database rename, or tracked-artifact removal.

## Priority 9D: Release Database Relocation Workflow
- **Goal**: Implement a verified release database relocation workflow that preserves the source database and enables `UseReleaseDataRoot` only after successful verification.
- [x] Add `IDatabaseRelocationService` and `DatabaseRelocationService`.
- [x] Validate the live source database with `PRAGMA integrity_check` before relocation.
- [x] Create a mandatory safety backup before relocation.
- [x] Create a stable source snapshot before copying the database to the target.
- [x] Preserve any existing target by snapshotting it before overwrite and restore it on failure.
- [x] Verify the target database integrity and SHA256 checksum against the stable source snapshot after copy.
- [x] Update `RuntimeDataRoot`, `UseReleaseDataRoot`, `RuntimeStorageMode`, and `DatabaseFileName` only after target verification succeeds.
- [x] Keep `DatabaseFileName` as `KicsitLibrary.db` and preserve current development behavior unless relocation is explicitly performed.
- [x] Two hundred forty-three isolated SQLite tests pass with completion of the relocation workflow.

Deferred refinements:
- [ ] Add explicit published UI guidance for relocation and release-root enablement in the next update.
- [ ] Add WPF UI automation coverage for the relocation workflow once the release path has been validated.
- [ ] Integrate report exports, certificates, ownership locks, logs, and temp folders with `IRuntimePathService` only after manual validation.
- [ ] Remove already tracked generated artifacts with `git rm --cached` only after explicit approval.
- [ ] Decide per-user versus per-machine runtime data root for installer deployment.

## Priority 10A: Settings Management UI
- **Goal**: Implement a settings screen in the admin portal for database, overdue, and email settings.
- [x] Create Settings screen UI and view models.
- [x] Bind configuration properties directly to sqlite settings.

## Phase 11: Catalog and Circulation QA Implementation
- **Goal**: Resuce and stabilize catalog and circulation fixes based on librarian QA feedback.
- [x] Connect Dashboard to real sqlite database statistics.
- [x] Implement case-insensitive search and duplicate blocking for Authors and Publishers.
- [x] Block deletion of linked Authors and Publishers with friendly warning messages.
- [x] Implement parent-child category relations and sorting.
- [x] Implement Department management CRUD and case-insensitive duplicate blocking.
- [x] Expand Book Copies to support Rack, Shelf, Source, Remarks, and duplicate accession blocking.
- [x] Add filters to Overdue reminders (Department, Date Range, Active only, Unresolved only).
- [x] Expose user-friendly columns in Overdue grid and hide internal ID.
- [x] Extend Receive and Return workflow to support Pay Now, Pay Later, and Waive modes.
- [x] Block student duplicate email registration at VM and Service layer.
- [x] Add CNIC, email, and phone validation to Student form.
- [x] Add 25 new tests; all 284 tests pass successfully.

## Phase 12A: Release Packaging and Deployment Dry Run
- **Goal**: Validate release-ready settings, compilation, and file distribution.
- [x] Enhance `deployment_smoke_test.ps1` to run clean, restore, build, test, publish, and check files.
- [x] Execute clean and restore solution.
- [x] Build solution and run 284 unit tests.
- [x] Execute framework-dependent `dotnet publish` to smoke publish target.
- [x] Verify published folder contains executable, config, DLLs, and no development database.
- [x] Create `PHASE 12A DEPLOYMENT DRY RUN REPORT.md` comparing packaging formats (Portable, ClickOnce, MSIX, MSI) and make recommendations.
- [x] Update project documentation (Status, Tasks, Issues, Audit, Test commands, Readiness, Packaging).

## Phase 12B: Release Installer and Automatic Update Configuration
- [x] Run baseline compilation and automated verification tests (all 284 tests pass).
- [x] Verify product name (Ilm-o-Kutub System), company, copyright, version metadata, and runtime paths.
- [x] Create `PHASE 12B CLICKONCE INSTALLER PLAN.md` with versioning, update polling, prerequisites, signing, rollback, data safety, and VS publishing instructions.
- [x] Update project documentation (Status, Tasks, Issues, Audit, Test commands, Readiness, Packaging).

## Phase 12C: Final Release Documentation and GitHub README Preparation
- [x] Create final `README.md` in repository root.
- [x] Create `RELEASE NOTES.md` detailing internal build statuses.
## Phase 12D: Release Security Hardening & Credential Sanitization (Patch)
- **Goal**: Make the project completely safe for GitHub upload, university demo, and internal deployment.
- [x] Run full repository secret scan.
- [x] Sanitize all documentation, reports, and templates to remove plaintext passwords (README, Handover, Demo Checklist, Handoff, private credentials template).
- [x] Update and harden `scripts/security_scan.ps1` to run 10 checks recursively.
- [x] Implement redaction inside the security scan script so it never prints actual secret values.
- [x] Resolve gotchas in `security_scan.ps1` regarding single-line file parsing and root file scanning.
- [x] Expand integration tests to verify all markdown files do not expose passwords, settings exports mask SMTP passwords, activity logs mask SMTP passwords, and security scan script exists and output is redacted.
- [x] Verify build compiles with 0 errors and all 302 tests pass successfully.
- [x] Verify deployment smoke test passes.
- [x] Verify security scan script passes.

## Phase 12E: GitHub Push and Final Repository Preparation
- **Goal**: Safely package, audit, and push repository to remote origin.
- [x] Read all release checklists and documentation files.
- [x] Execute clean, restore, build, tests (302 passed), smoke test, and security scan.
- [x] Perform forensic audit on git tracked files (`git status` and `git ls-files`).
- [x] Verify `.gitignore` ignores all build, database, cert, and local user settings files.
- [x] Create `GITHUB PUSH CHECKLIST.md` for pre-push and rename guidelines.
- [x] Create `PHASE 12E GITHUB PUSH PREPARATION REPORT.md` listing validation results.
- [x] Confirm Git origin remote is configured (`main` branch).
- [x] Prepare commit message (`Finalize Ilm o Kutub System release documentation and security hardening`) and safe push command.
- [x] Keep push execution pending user approval.

## Phase 12G: Release Version Tagging
- **Goal**: Create and push an annotated Git tag for the current stable university demo build without creating a public binary release yet.
- [x] Execute clean, restore, build, tests (302 passed), smoke test, and security scan.
- [x] Verify target commit hash (d2022b4).
- [x] Verify no existing tags conflict.
- [x] Create annotated tag `v1.0.0-demo` locally.
- [x] Push `v1.0.0-demo` to remote origin.

## Priority 8E+: Sync & Deployment
- **Goal**: Add each remaining system utility as a separate, safety-reviewed task.
- [ ] Integrate Supabase Sync: push local updates to Supabase cloud database to support remote sync backups.
- [ ] Configure `appsettings.json` encryption routines for sensitive credentials.
- [ ] Implement DPAPI encryption for SMTP password at rest.
- [ ] Add `MustChangePassword` flag to User entity and enforce change on first login for seeded accounts.
- [ ] Implement final production installer or ClickOnce publish to the university server using the manual steps.


---

## Final Release Documentation
- [x] `README.md` generated and sanitized.
- [x] `RELEASE NOTES.md` generated.
- [x] `DEMO CHECKLIST.md` generated and sanitized.
- [x] `INSTALLATION GUIDE.md` generated.
- [x] `SCREENSHOTS GUIDE.md` generated.
- [x] `SECURITY CHECKLIST.md` generated.
- [x] `RELEASE SECURITY NOTES.md` generated.
- [ ] At final release, apply university code-signing certificate for ClickOnce deployment.
- [ ] GitHub repository rename to `Ilm-o-Kutub-System` (manual owner action).
