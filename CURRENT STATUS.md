# Current Project Status

This document catalogs all implemented and pending files, services, entities, ViewModels, and views inside Ilm-o-Kutub System.

---

## 1. Summary of Progress
- **Phase A (Codebase Audit & Core Refactoring)**: **100% Completed**
- **Priority 1 (Library Catalog Management)**: **100% Completed**
- **Priority 2 (Consumer Management)**: **100% Completed**
- **Priority 3 (Circulation System)**: **100% Completed**
- **Priority 4A (Database Initialization & Test Infrastructure)**: **100% Completed**
- **Priority 4B (Deterministic Overdue & Notification Records)**: **100% Completed**
- **Priority 4C (Manual SMTP Delivery & Retry)**: **100% Completed**
- **Priority 4D (Cancellation-Aware Background Scheduler)**: **100% Completed**
- **Priority 5A (Reports & Export Foundation)**: **100% Completed**
- **Priority 5B (Advanced Reports & Print Refinements)**: **100% Completed**
- **Priority 6A (Student & Faculty Clearance Workflow)**: **100% Completed**
- **Priority 6B (Reservation Workflow Completion)**: **100% Completed**
- **Priority 7A (Activity Log Browser & Audit Records Workflow)**: **100% Completed**
- **Priority 7B (Inventory Management & Physical Stock Verification)**: **100% Completed**
- **Priority 8A (Verified Local SQLite Backup Creation)**: **100% Completed**
- **Priority 8B (Verified Local SQLite Restore)**: **100% Completed**
- **Product Branding & Management UI Refinement**: **100% Completed**
- **Priority 8C (Automatic Backup Scheduling & Retention Safety Policy)**: **100% Completed**
- **Priority 8D (Cross Process Database and Backup Ownership Protection)**: **100% Completed**
- **Priority 9A (Secure Document Upload Workflow)**: **100% Completed**
- **Priority 9B (Deployment Preparation Audit & Release Readiness Plan)**: **100% Completed**
- **Priority 9C (Release Data Location & Source Control Cleanup Plan)**: **100% Completed**
- **Priority 9D (Release Database Relocation Workflow)**: **100% Completed**
- **Priority 9E (Source Control Cleanup Execution)**: **100% Completed**
- **Priority 8E+ (Sync & Deployment)**: **Pending Implementation**

### Product Branding and UI Refinement
- The visible product name is now **Ilm-o-Kutub System** across the shell, login, dashboard welcome state, library card, report/PDF/Excel output, certificate output, backup metadata, restore metadata, application settings defaults, and assembly product metadata.
- Internal projects, namespaces, executable assembly name, solution name, and `KicsitLibrary.db` remain unchanged for compatibility and data safety.
- `ProductBrand` is the centralized source for product, repository, institution, and default artifact-folder names.
- The global palette now uses off-white application surfaces, charcoal navigation, muted blue actions, teal operational accent, restrained semantic colors, dark slate text, and soft gray borders.
- Main navigation labels are now concise: Dashboard, Book Catalog, Issue Material, Receive Material, Reservations, Fines, Overdue Reminders, Notifications, Students, Faculty and Staff, Visits, Library Clearance, Reports, Audit Records, Activity Logs, Inventory, Stock Verification, Backup, Restore, and Settings.
- **Show Helpful Hints** is available in the main top bar, defaults to enabled, updates the current session immediately, and controls practical WPF tooltips on navigation and important actions.
- Reports and clearance certificates include the product name while preserving the institution name as a separate identity.
- New backups default to `Documents\Ilm-o-Kutub Backups`; reports to `Documents\Ilm-o-Kutub Reports`; certificates to `Documents\Ilm-o-Kutub Certificates`.
- Four branding/hint regression tests were added during the branding phase. The full isolated suite now passes with 220 tests after Priority 9A.
- Automatic backup scheduling and retention safety are implemented. Deployment, Supabase sync, EF migrations, WhatsApp delivery, and final README generation were not started.
- Priority 9B deployment preparation is complete as documentation and smoke-test scripting only. No installer, ClickOnce package, MSIX package, production publish, Supabase sync, EF migrations, WhatsApp delivery, final README, repository rename, namespace rename, or database rename was performed.
- Priority 9C adds a guarded runtime data-location service and source-control cleanup plan only. Default development startup still uses executable-relative `KicsitLibrary.db`; no database relocation, tracked-artifact removal, installer packaging, EF migration, Supabase sync, WhatsApp delivery, or final README was performed.
- Priority 9D completes the verified release database relocation workflow. It copies the source database through a stable snapshot, verifies both source and target SQLite integrity, compares SHA256 checksums, preserves the source database by default, creates a mandatory safety backup, preserves and restores an existing target on failure, and enables `UseReleaseDataRoot` only after successful verification. `DatabaseFileName` remains `KicsitLibrary.db`. Default development behavior remains unchanged unless relocation is explicitly requested.
- Priority 9E executes source control cleanup. It untracked 1082 generated artifacts (.vs/ IDE cache: 14 files, bin/ build output: 407 files, obj/ build intermediate: 661 files) using non-destructive `git rm --cached` commands that preserve all local files on disk. Updated `.gitignore` with patterns for TestResults/, *.trx, *.coverage, *.coveragexml, *.nupkg, Temp/, and Locks/. All local files including database, backups, documents, reports, and certificates remain preserved. Build passes (0 warnings, 0 errors); all 243 tests still pass. Future generated artifacts will not be tracked per the updated .gitignore patterns.

### GitHub Repository Rename
The GitHub repository rename remains a manual owner action. The target repository name is `Ilm-o-Kutub-System`.

Checklist:
1. Go to the GitHub repository.
2. Open **Settings**.
3. Rename the repository to `Ilm-o-Kutub-System`.
4. Update the local remote URL after rename if needed.
5. Verify push and pull still work.
6. Generate the final `README.md` only during the final release phase.

Documented CLI alternative, to run only after authentication and explicit confirmation:

```powershell
gh repo rename Ilm-o-Kutub-System --repo OWNER/CURRENT_REPOSITORY
```

### Priority 4A Foundation
- Startup uses `EnsureCreatedAsync` only. EF migrations remain deliberately deferred.
- Relative SQLite paths are resolved from the desktop executable directory.
- Fatal database initialization or seeding failures stop application startup.
- `KicsitLibrary.Tests` is a real xUnit test project with nine passing tests.
- Tests use isolated temporary SQLite files and never use the development database.
- Shared overdue-day and fine calculations are available through `OverdueCalculator`.

### Priority 4B Foundation
- Active overdue items are derived from `IssueRecord.ReceiveRecord == null` and local due-date rules.
- `OverdueService` creates idempotent `InApp` and `Email` notification records manually.
- Same-day and configurable cooldown checks prevent duplicate records.
- Email delivery remains pending; no SMTP client or hosted scheduler exists.
- Missing member email creates a failed record without crashing processing.
- Notification retry and read-state management are persisted and audited.
- Overdue Reminders and Notification Center views are wired into navigation.
- Dashboard overdue totals now use active issue due dates instead of `BookCopy.Overdue`.
- Nineteen isolated SQLite tests pass.

### Priority 4C Foundation
- `NotificationService` sends selected or all pending email records only through `IEmailTransport`.
- `MailKitEmailTransport` provides fully asynchronous SMTP delivery; tests use `FakeEmailTransport`.
- SMTP settings are read from `SystemSettings` and can be validated from Notification Center.
- Email delivery is manual. No hosted service, timer, startup send, or automatic overdue send exists.
- Successful attempts persist `Sent`, `SentAt`, `LastAttemptAt`, and retry metadata.
- Failed attempts persist `Failed` and a sanitized `FailureReason`; disabled delivery remains pending.
- In-app records never pass through SMTP, and WhatsApp remains a placeholder.
- Notification Center exposes send selected, retry selected, send all pending, validate settings, refresh, and mark-read actions.
- Twenty-nine isolated SQLite tests pass, including ten fake-transport delivery tests.

### Priority 4D Foundation
- `OverdueSchedulerService` coordinates scheduled and bulk manual overdue runs through one process-level semaphore.
- Every run creates a fresh DI scope for EF, overdue, notification, and activity-log services.
- `OverdueSchedulerBackgroundService` waits for database initialization before reading settings.
- The worker supports disabled idle polling, optional delayed startup runs, configurable periodic intervals, and cooperative shutdown cancellation.
- Scheduler, startup-run, and automatic-email settings are disabled by default.
- Runs persist last-run, success, failure, message, and running-state settings.
- SQLite busy/locked operations receive three bounded retries with cancellation-aware short delays.
- The Overdue Reminders screen shows scheduler status and provides real run-now and refresh actions.
- Bulk manual overdue checks use the same coordinator lock but never enable automatic email delivery.
- Thirty-nine isolated SQLite tests pass, including ten scheduler tests.

### Priority 5A Foundation
- Added data-first report contracts, definitions, filters, rows, results, and export results.
- Five scoped providers query real SQLite data for Catalog, Issued Books, Overdue Books, Fines, and Notifications.
- CSV, Excel, and PDF exporters consume `ReportResult` only and never query the database.
- ClosedXML generates `.xlsx` files without requiring Microsoft Excel.
- CSV and PDF generation use internal deterministic writers with no additional runtime applications.
- Export paths default to the current user's Documents folder under `Ilm-o-Kutub Reports`.
- File names are sanitized, timestamped, and made unique unless overwrite is explicitly requested.
- Successful and failed export actions write activity-log records.
- Reports & Analytics navigation opens a real dashboard with report-specific filters, dynamic preview, empty state, and export actions.
- Fifty-two isolated tests pass, including thirteen reporting and physical-file tests.

### Priority 5B Advanced Reports
- Added eleven production providers for clearance, borrowing history, reservations, lost/damaged books, deleted-book archives, visits, audits, inventory, new arrivals, and stock verification.
- All sixteen reports query real SQLite entities through the existing data-first provider contracts.
- The dashboard groups reports into seven categories, supports report search/count, and renders text, enum, date-range, number-range, and boolean filters from definitions.
- Invalid date and number ranges produce clear validation messages before querying.
- PDF output now repeats report metadata and table headers, handles wide reports in landscape, and adds page numbers and summaries.
- Excel output includes metadata, styled headers, frozen rows, auto filters, bounded auto-fit widths, date/currency formats, and summaries.
- CSV output preserves safe escaping, headers for empty reports, and deterministic date formatting.
- Sixty-eight isolated tests pass, including sixteen Priority 5B tests.

### Priority 6A Clearance Workflow
- Added transaction-safe student and faculty/staff clearance checking, approval, revocation, history, and certificate generation.
- Active issues, unpaid/partial fines, and unresolved lost/damaged/missing/repair cases block approval with exact item details.
- Approval requires remarks and the authenticated user; revocation requires a reason.
- Student and faculty/staff records persist clearance status, date, remarks, and approving user ID.
- Existing SQLite databases receive only additive clearance columns and status indexes through `DatabaseCompatibilityInitializer`.
- Clearance certificates are real PDF files under `Documents\Ilm-o-Kutub Certificates` by default and are revalidated before export.
- Approval, revocation, and certificate generation write activity logs.
- Library Clearance navigation opens a real dashboard with student/faculty worklists, filters, validation details, actions, and borrowing/history dialog.
- The Student Clearance Report now includes unresolved lost/damaged case counts.
- Eighty-two isolated tests pass, including fourteen Priority 6A tests.

### Priority 6B Reservation Workflow
- Added complete student and faculty/staff reservation eligibility, creation, queue, cancellation, expiry, availability, fulfillment, and query services.
- Active, uncleared members are eligible only when they have no duplicate reservation, active issue for the same title, or unpaid/partial fine.
- Queue order is deterministic by reservation date and record ID; only the first active queue member can be fulfilled.
- Reservation expiry uses `ReservationExpiryDays`, defaulting safely to three days when the setting is absent or invalid.
- Returned available copies can mark the first queued reservation `Available` without automatically issuing the book.
- Availability creates deduplicated in-app and email notification records; no email is sent automatically.
- Missing recipient email creates a clear failed email record while preserving the in-app notification.
- Fulfillment reuses circulation validation and issue creation, assigns an available copy, stores the accession number, and marks the reservation `Issued`.
- Added Reservation Management, Create Reservation, and Reservation Queue MVVM screens with real asynchronous actions.
- Reservation reporting now includes queue position and lifecycle-status summaries.
- One hundred isolated SQLite tests pass, including eighteen Priority 6B tests.

### Priority 7A Activity Logs and Audit Records
- Added a read-only-by-default activity log browser with latest-500 loading, action/entity/user/date filters, full details, summaries, and distinct filter values.
- Activity metadata is derived safely from existing structured detail fields without adding database columns.
- Activity log snapshots export through the existing CSV, Excel, and PDF report exporters.
- Old-log deletion exists only as a protected service operation for Super Admin/Admin; it soft-deletes rows, archives a range summary, and logs the action.
- Added transaction-safe audit record creation, update, status change, and soft deletion with archive snapshots and activity logs.
- Audit numbers are checked for uniqueness and required audit number, date, type, and status fields are validated.
- Status changes and deletion require remarks/reasons.
- Audit attachments are displayed read-only because the existing `AuditFile` entity has no soft-delete metadata.
- Added Activity Logs and Audit Records navigation, searchable grids, detail dialogs, audit form, status/delete actions, and report exports.
- Authorization grants full access to Super Admin/Admin, permission-based management to Librarian, view-only access to Auditor, and permission-based view access to other roles.
- The Audit Report continues to use the existing report foundation and now includes audit number.
- One hundred sixteen isolated SQLite tests pass, including sixteen Priority 7A tests.

### Priority 7B Inventory and Stock Verification
- Added authorization-aware inventory listing, details, create, update, quantity adjustment, damage, repair, soft-delete, restore, summaries, activity logs, and report export.
- Added persisted stock-verification sessions and entries with expected/actual status, mismatch remarks, completion summaries, bulk missing marking, and explicit-only reconciliation.
- Existing SQLite databases receive two new tables and unique indexes through the additive compatibility initializer; no migrations or destructive operations were added.
- Added real Inventory Management and Stock Verification MVVM views, forms, detail dialogs, navigation, and DI registrations.
- Inventory and Stock Verification reports now reflect current inventory and latest persisted verification results.
- Document attachment metadata remains read-only; the upload/remove workflow is deferred.
- One hundred thirty-five isolated SQLite tests pass, including nineteen Priority 7B tests.

### Priority 8A Verified Local SQLite Backup Creation
- Added manual online SQLite backups using `Microsoft.Data.Sqlite.SqliteConnection.BackupDatabase`; the live database file is not copied directly.
- Every backup receives a timestamped, sanitized, non-overwriting file name under `Documents\Ilm-o-Kutub Backups` by default.
- Backup verification reopens the generated database read-only, runs `PRAGMA integrity_check`, and computes a SHA-256 checksum.
- Metadata JSON excludes system settings and SMTP credentials; optional ZIP compression includes the database and metadata while retaining the original database file.
- Added persisted backup history, status summaries, filters, soft-delete support, and activity logs for creation, verification, compression failure, authorization denial, and history deletion.
- Existing SQLite databases receive the `BackupHistories` table and indexes through the additive compatibility initializer; no migrations or destructive schema changes were added.
- Added a real Backup Management MVVM screen, details dialog, navigation, and DI registrations.
- Super Admin/Admin can create and verify backups. Librarian/Auditor can view history. Other roles require the seeded backup permissions.
- One hundred forty-eight isolated SQLite tests pass, including thirteen Priority 8A tests.

### Priority 8B Verified Local SQLite Restore
- Added verified restore preview, validation, history, status summaries, authorization, and activity logging.
- Restore accepts only non-empty SQLite files that pass `PRAGMA integrity_check`, SHA-256 hashing, and required application schema-table checks.
- Every approved restore creates a verified online safety backup before staging any replacement.
- The active database is never overwritten while WPF, hosted services, or scoped DbContexts are running.
- A verified staged copy and non-sensitive pending metadata are written beside the configured database; application restart is required.
- Startup applies a pending restore before the host or any DbContext starts, creates an emergency copy, performs post-replacement integrity checks, and rolls back on simulated or real failure.
- Startup restore results are imported into `RestoreHistories` and `ActivityLogs` after compatibility initialization.
- Added additive `RestoreHistories` compatibility SQL and indexes without migrations or destructive schema changes.
- Added Restore Management, restore preview/confirmation, restore history, and Backup Management integration.
- Super Admin/Admin can stage restores. Librarian/Auditor can view restore history. Other roles require explicit permissions.
- One hundred sixty-seven isolated SQLite tests pass, including nineteen Priority 8B tests.

### Priority 8C Automatic Backup Scheduling and Retention Safety Policy
- Added disabled-by-default automatic backup settings in `SystemSettings` for enablement, startup run, interval, startup delay, compression, verification, destination folder, retention policy, physical deletion, and last-run status.
- Added `IAutomaticBackupSchedulerService`, `AutomaticBackupSchedulerService`, `AutomaticBackupBackgroundService`, `AutomaticBackupStartupSignal`, `IBackupRetentionService`, and `BackupRetentionService`.
- Automatic backup creates a fresh DI scope per run, uses one scheduler semaphore, honors cancellation, waits until database initialization and login complete, and calls the existing real `IBackupService` for online SQLite backup, verification, compression, history, and logging.
- Pending restore metadata causes automatic backup to skip and log a clear status instead of touching backup files.
- Retention is disabled by default and performs history-only soft deletion unless physical deletion is explicitly enabled.
- Retention protects the live database, latest successful backup, failed-verification backups, restore safety backups, emergency restore files, pending restore files, unsupported extensions, files outside the configured backup folder, and detectable symbolic/reparse paths.
- Backup Management now includes scheduler status, settings, run-now, refresh, save, retention preview/apply, open-folder actions, physical deletion warning, and a retention candidate grid.
- Super Admin/Admin can configure, run, and apply retention. Librarian/Auditor can view backup history and scheduler status. Other roles are blocked unless explicit permissions are granted.
- Test databases now use unique temporary directories so pending restore metadata cannot race between test classes.
- One hundred eighty-eight isolated SQLite tests pass, including seventeen Priority 8C scheduler/retention tests.

### Priority 8D Cross Process Ownership Protection
- Added `IDatabaseOwnershipService` / `DatabaseOwnershipService` with an application instance mutex plus per-domain critical operation lease files for database, backup, restore, and scheduler operations.
- Ownership settings are seeded non-destructively: `SingleInstanceMode=True`, `CriticalOperationLockTimeoutSeconds=15`, `AllowReadOnlySecondInstance=False`, `CleanupStaleLockFilesOnStartup=True`, and `LockFileRetentionMinutes=120`.
- Critical operation lease metadata includes operation name, process ID, machine name, user name, acquired time, expiry time, lock name, and lock file path. It does not include SMTP passwords, database passwords, connection strings, or system-setting values.
- Backup creation, restore staging, pending restore startup application, automatic scheduler runs, retention physical deletion, and SQLite compatibility initialization use ownership locks where applicable.
- Lock timeout failures return the clear message: `Another Ilm-o-Kutub System operation is already using this database or backup folder.`
- Health checks now inspect separate application, database, backup, restore, and scheduler domains instead of reporting every domain from one lock probe.
- Stale cleanup deletes only expired safe lock files or old unreadable files that can be opened exclusively; active locks are never deleted.
- Unauthorized cleanup attempts are logged. Admin/Super Admin can cleanup. Librarian/Auditor can view ownership status through Backup Management but cannot cleanup.
- Backup Management now includes ownership status, last ownership message, stale lock count, refresh status, and cleanup stale lock file actions.
- Two hundred three isolated SQLite tests pass, including fifteen real Priority 8D ownership tests.

### Priority 9A Secure Document Upload Workflow
- Added managed document upload models, `IDocumentService`, and `IDocumentStorageService` for administrative documents.
- Supported document types are Library SOP, National Library Rates, Library Policy, Audit Evidence, Visit Evidence, Invoice, Inventory Document, and General Document.
- Upload validation requires a title, known document type, existing source file, configured max size, extension allowlist, blocked executable/script extensions, and basic signatures for PDF, PNG, JPG/JPEG, DOCX, and XLSX.
- Files are copied to managed storage outside the source tree. The default root is `%USERPROFILE%\Documents\Ilm-o-Kutub System\Documents`, or `SystemSettings.DocumentStorageRoot` when configured.
- Stored names are generated, timestamped, non-overwriting, and independent of user-supplied filenames. SHA-256, size, original file name, content type, and related entity metadata are persisted.
- Soft delete and restore affect records only by default; physical file deletion remains deferred.
- Missing physical files keep their database records and show a `Missing File` status.
- Added non-destructive SQLite compatibility support for the `DocumentUploads` table, additive columns, and indexes without EF migrations.
- Added document permissions and settings to `DbSeeder` without overwriting existing customized settings.
- Added real Documents navigation with management grid, filters, upload dialog, details dialog, open, copy, soft delete, restore, and clear-filter actions.
- Added SOP Documents and National Library Rates Documents report providers while preserving the existing report foundation.
- Audit, Visit, and Inventory integration is provided through `RelatedEntityType` and `RelatedEntityId`; full inline module detail embedding is deferred.
- Two hundred twenty isolated SQLite tests pass, including seventeen Priority 9A document workflow tests.

### Priority 9B Deployment Preparation Audit
- Added `DEPLOYMENT READINESS AUDIT.md` covering project structure, runtime, entry point, database strategy, SQLite location, backup/restore, document storage, report folders, settings, authentication notes, test count, commands, blockers, packaging options, risk matrix, and release checklist.
- Added `PACKAGING STRATEGY.md` covering portable, self-contained, framework-dependent, ClickOnce, MSIX, and Windows Installer options.
- Added `RELEASE TEST PLAN.md` with fresh install, first-run database creation, login, catalog, consumer, circulation, reservation, clearance, reports, backup, restore, automatic backup, ownership, documents, audit logs, settings, uninstall, upgrade, offline, and recovery checks.
- Added `scripts/deployment_smoke_test.ps1` to run build, tests, and a local desktop publish into `artifacts/deployment-smoke/publish` without launching the app or creating an installer.
- Added `.gitignore` for future build outputs, local runtime databases, publish artifacts, backups, documents, reports, certificates, logs, and IDE files.
- Added root `Directory.Build.props` to centralize release metadata: product, company, copyright, and version.
- Source-control audit found `bin`, `obj`, `.vs`, and local database artifacts are already tracked. No tracked files were removed in Priority 9B; cleanup remains a separate approved task.
- Packaging remains pending. Final README remains pending. GitHub repository rename remains manual.

### Priority 9C Release Data Location and Source Control Cleanup Plan
- Added `IRuntimePathService` and `RuntimePathService` to centralize runtime data paths.
- Added non-destructive runtime `SystemSettings` defaults for data root, storage mode, release-root guard, database file name, and folder names for documents, backups, reports, certificates, restore staging, logs, temp files, and locks.
- Default development mode preserves the executable-relative SQLite database path and does not move or rename `KicsitLibrary.db`.
- Release mode can resolve the runtime data root to `%LOCALAPPDATA%\Ilm-o-Kutub System`, or to a configured `RuntimeDataRoot`.
- Document storage and backup defaults now use the runtime path service only when their explicit settings are empty.
- Restore staging uses the runtime staging path only when the runtime database path matches the active SQLite database path; otherwise existing beside-database pending restore behavior is preserved for startup compatibility.
- Added `RUNTIME DATA LOCATION STRATEGY.md` and `SOURCE CONTROL CLEANUP PLAN.md`.
- Updated the deployment smoke script to report runtime data mode, publish output, and non-destructive limitations.
- Added eight isolated runtime path tests.
- Source-control cleanup remains a plan only; tracked generated artifacts were not removed.

### Priority 9D Release Database Relocation Workflow
- Added `IDatabaseRelocationService` and `DatabaseRelocationService` to implement a verified release database relocation workflow.
- Added a mandatory verified safety backup before relocation, source integrity validation, and release-specific target verification.
- Added stable source snapshot creation before copy, SHA256 checksum comparison against the source snapshot, and optional release runtime settings updates only after successful verification.
- Added preservation of an existing target by snapshotting it before overwrite and rollback/restore behavior on failure.
- Kept `DatabaseFileName` as `KicsitLibrary.db`, preserved the source database by default, and preserved current development startup behavior unless relocation is explicitly performed.
- Two hundred forty-three isolated SQLite tests pass, including the completion of the release database relocation workflow.

---

## 2. Completed Components

### Entities (`KicsitLibrary.Core/Entities/`)
- `EntityBase.cs`: Baseline identifier (`Id`), audit properties (`CreatedAt`, `UpdatedAt`), and soft-delete tracker (`IsDeleted`).
- `BookMaster.cs` & `BookCopy.cs`: Catalog details and physical copy tracking.
- `Student.cs` & `FacultyStaff.cs`: Member directories.
- `Fine.cs`: Fine records, payment balances, and waiver logs.
- `IssueRecord.cs`: Check-out history.
- `Reservation.cs`: Books holds and queue entries.
- `SystemSettings.cs`: Dynamic business rule settings.
- `User.cs` & `Role.cs`: Authentication and authorization.
- `VisitRecord.cs`: Visitation inspector logs.

### Interfaces & Services (`KicsitLibrary.Core/Interfaces/` & `KicsitLibrary.Services/`)
- `IAuthenticationService.cs` / `AuthenticationService.cs`
- `IDashboardService.cs` / `DashboardService.cs`
- `ICatalogService.cs` / `CatalogService.cs`
- `IConsumerService.cs` / `ConsumerService.cs`
- `ICirculationService.cs` / `CirculationService.cs`
- `IOverdueService.cs` / `OverdueService.cs`
- `INotificationService.cs` / `NotificationService.cs`
- `IEmailTransport.cs` / `MailKitEmailTransport.cs`
- `IEmailSettingsService.cs` / `EmailSettingsService.cs`
- `IOverdueSchedulerService.cs` / `OverdueSchedulerService.cs`
- `OverdueSchedulerBackgroundService.cs`
- `IReportService` / `ReportService`
- `IReportDataProvider` / sixteen production report providers
- `IReportExporter` / CSV, Excel, and PDF exporters
- `IClearanceService.cs` / `ClearanceService.cs`
- `IReservationService.cs` / `ReservationService.cs`
- `IActivityLogBrowserService.cs` / `ActivityLogBrowserService.cs`
- `IAuditRecordService.cs` / `AuditRecordService.cs`
- `IBackupService.cs` / `BackupService.cs`
- `IAutomaticBackupSchedulerService.cs` / `AutomaticBackupSchedulerService.cs`
- `IBackupRetentionService.cs` / `BackupRetentionService.cs`
- `IDatabaseOwnershipService.cs` / `DatabaseOwnershipService.cs`
- `IDocumentService.cs` / `DocumentService.cs`
- `IDocumentStorageService.cs` / `DocumentStorageService.cs`

### ViewModels (`KicsitLibrary.Desktop/ViewModels/`)
- `MainViewModel.cs`: Shell navigation controller.
- `LoginViewModel.cs`: Core authentication viewmodel.
- `DashboardViewModel.cs`: Basic dashboard summaries.
- `BookCatalogViewModel.cs` / `BookFormViewModel.cs` / `CopiesViewModel.cs` / `AuthorViewModel.cs` / `PublisherViewModel.cs`: Catalog module.
- `StudentsManagementViewModel.cs` / `StudentFormViewModel.cs` / `FacultyStaffManagementViewModel.cs` / `FacultyStaffFormViewModel.cs` / `ConsumerProfileViewModel.cs` / `VisitRecordsViewModel.cs` / `VisitRecordFormViewModel.cs`: Consumer module.
- `IssueMaterialViewModel.cs` / `ReceiveMaterialViewModel.cs` / `FinesManagementViewModel.cs`: Circulation module.
- `OverdueRemindersViewModel.cs` / `NotificationCenterViewModel.cs`: Manual overdue and notification-record operations.
- `ReportsDashboardViewModel.cs` / `ReportPreviewViewModel.cs`: Report selection, filters, preview, and export operations.
- `ClearanceDashboardViewModel.cs` / `StudentClearanceViewModel.cs` / `FacultyStaffClearanceViewModel.cs` / `ClearanceDetailsViewModel.cs`
- `ReservationManagementViewModel.cs` / `ReservationFormViewModel.cs` / `ReservationQueueViewModel.cs`
- `ActivityLogsViewModel.cs` / `ActivityLogDetailsViewModel.cs`
- `AuditRecordsViewModel.cs` / `AuditRecordFormViewModel.cs` / `AuditRecordDetailsViewModel.cs`
- `BackupManagementViewModel.cs` / `BackupDetailsViewModel.cs`: manual backup, automatic backup scheduler, retention controls, and database/backup ownership status controls.
- `DocumentManagementViewModel.cs` / `DocumentUploadViewModel.cs` / `DocumentDetailsViewModel.cs`: secure document listing, validation/upload, metadata details, open/copy, soft delete, and restore actions.

### Views (`KicsitLibrary.Desktop/Views/` & Root)
- `MainWindow.xaml` / `MainWindow.xaml.cs`: Primary shell window.
- `LoginWindow.xaml` / `LoginWindow.xaml.cs`: Hashed credentials authorization dialog.
- `DashboardView.xaml`: Stats and indicators.
- `BookCatalogView.xaml` / `BookFormWindow.xaml` / `CopiesWindow.xaml` / `AuthorWindow.xaml` / `PublisherWindow.xaml`
- `StudentsManagementView.xaml` / `StudentFormWindow.xaml` / `FacultyStaffManagementView.xaml` / `FacultyStaffFormWindow.xaml` / `ConsumerProfileWindow.xaml` / `VisitRecordsView.xaml` / `VisitRecordWindow.xaml`
- `IssueMaterialView.xaml` / `ReceiveMaterialView.xaml` / `FinesManagementView.xaml`
- `OverdueRemindersView.xaml` / `NotificationCenterView.xaml`
- `ReportsDashboardView.xaml` / `ReportPreviewView.xaml`
- `ClearanceDashboardView.xaml` / `StudentClearanceView.xaml` / `FacultyStaffClearanceView.xaml` / `ClearanceDetailsWindow.xaml`
- `ReservationManagementView.xaml` / `ReservationFormWindow.xaml` / `ReservationQueueWindow.xaml`
- `ActivityLogsView.xaml` / `ActivityLogDetailsWindow.xaml`
- `AuditRecordsView.xaml` / `AuditRecordFormWindow.xaml` / `AuditRecordDetailsWindow.xaml`
- `BackupManagementView.xaml` / `BackupDetailsWindow.xaml`
- `DocumentManagementView.xaml` / `DocumentUploadWindow.xaml` / `DocumentDetailsWindow.xaml`

### Navigation & Routes Wired
- `"Dashboard"` -> `DashboardViewModel`
- `"Book Catalog"` -> `BookCatalogViewModel`
- `"Issue Material"` -> `IssueMaterialViewModel`
- `"Receive Material"` -> `ReceiveMaterialViewModel`
- `"Fines"` -> `FinesManagementViewModel`
- `"Students"` -> `StudentsManagementViewModel`
- `"Faculty and Staff"` -> `FacultyStaffManagementViewModel`
- `"Visits"` -> `VisitRecordsViewModel`
- `"Overdue Reminders"` -> `OverdueRemindersViewModel`
- `"Notifications"` -> `NotificationCenterViewModel`
- `"Reports"` -> `ReportsDashboardViewModel`
- `"Library Clearance"` -> `ClearanceDashboardViewModel`
- `"Reservations"` -> `ReservationManagementViewModel`
- `"Activity Logs"` -> `ActivityLogsViewModel`
- `"Audit Records"` -> `AuditRecordsViewModel`
- `"Inventory"` -> `InventoryManagementViewModel`
- `"Stock Verification"` -> `StockVerificationViewModel`
- `"Backup"` -> `BackupManagementViewModel`
- `"Restore"` -> `RestoreManagementViewModel`
- `"Documents"` -> `DocumentManagementViewModel`

---

## 3. Pending Components

- **Views & ViewModels**:
  - `SystemSettingsView.xaml` & `SystemSettingsViewModel.cs` (the Settings route exists, but the screen is not implemented)
- **Services**:
  - Supabase sync and deployment remain pending.
- **Final Release Documentation**:
  - Generate a complete professional repository-root `README.md` at final release so GitHub displays the project overview on the repository front page.
  - The final README must include the project title, overview, key features, technology stack, architecture, screenshots placeholder, installation guide, database setup, default login accounts, build and test commands, release notes, known limitations, future improvements, contributors, and license placeholder.
  - The project is still under active development, so the final `README.md` will be generated only after all main modules, testing, deployment, and release packaging are complete.
