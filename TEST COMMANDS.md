# Build, Test, and Database Inspection Commands

Use the following commands from the repository root (`c:\Project\University-Library-Management-system\`) to build, run, and maintain Ilm-o-Kutub System.

---

## 1. Project Compilation
Build the entire solution containing all projects:
```powershell
dotnet build KicsitLibrary.slnx
```

Clean the solution binaries:
```powershell
dotnet clean KicsitLibrary.slnx
```

---

## 2. Test Execution
Run the unit test project (ensure tests are discovered):
```powershell
dotnet test KicsitLibrary.slnx
```

Run test execution with logging:
```powershell
dotnet test KicsitLibrary.slnx --logger "console;verbosity=detailed"
```

The test project uses a unique temporary SQLite directory and file per test under `%TEMP%\KicsitLibrary.Tests`. It never opens `KicsitLibrary.db`.

Current expected result (post-Phase 12B Splash screen addition):

```text
Passed: 284
Failed: 0
Skipped: 0
Duration: ~40-60s
```

Post-Phase 12G note: All 302 tests pass successfully.
Phase 12G Release Version Tagging is complete. Annotated tag v1.0.0-demo was created and pushed. Build passed. Tests passed with exact count of 302. Security scan passed. No public binary release was created. Installer remains pending. Repository rename remains manual. Cloud sync remains pending.

Run only branding and helpful-hint tests:

```powershell
dotnet test KicsitLibrary.Tests/KicsitLibrary.Tests.csproj --filter "FullyQualifiedName~BrandingAndHintsTests"
```

These tests verify the centralized visible product name, key UI/configuration branding, the default-enabled hint preference, and off/on toggling.

Run only Priority 4B tests:

```powershell
dotnet test KicsitLibrary.Tests/KicsitLibrary.Tests.csproj --filter "FullyQualifiedName~OverdueNotificationTests|FullyQualifiedName~DashboardOverdueTests"
```

Run only Priority 4C email-delivery tests:

```powershell
dotnet test KicsitLibrary.Tests/KicsitLibrary.Tests.csproj --filter "FullyQualifiedName~EmailDeliveryTests"
```

Priority 4C tests use `FakeEmailTransport`; they never open a network connection or use the development database.

Run only Priority 4D scheduler tests:

```powershell
dotnet test KicsitLibrary.Tests/KicsitLibrary.Tests.csproj --filter "FullyQualifiedName~OverdueSchedulerTests"
```

Scheduler tests use temporary SQLite files, fake SMTP transport, and controllable overdue-service doubles. They do not wait for real scheduler intervals.

Run only Priority 5A report tests:

```powershell
dotnet test KicsitLibrary.Tests/KicsitLibrary.Tests.csproj --filter "FullyQualifiedName~ReportFoundationTests"
```

Report tests use temporary SQLite databases and temporary export folders. They physically create CSV, XLSX, and PDF files and do not require Microsoft Excel.

Run only Priority 5B report tests:

```powershell
dotnet test KicsitLibrary.Tests/KicsitLibrary.Tests.csproj --filter "FullyQualifiedName~Priority5BReportTests"
```

Priority 5B tests cover all eleven advanced providers, all sixteen registered definitions, wide PDF output, Excel metadata/headers, and empty/date-formatted CSV output.

Run only Priority 6A clearance tests:

```powershell
dotnet test KicsitLibrary.Tests/KicsitLibrary.Tests.csproj --filter "FullyQualifiedName~ClearanceWorkflowTests"
```

Clearance tests use isolated temporary SQLite databases and temporary certificate folders. They do not access the development database or require a printer.

Run only Priority 6B reservation tests:

```powershell
dotnet test KicsitLibrary.Tests/KicsitLibrary.Tests.csproj --filter "FullyQualifiedName~ReservationWorkflowTests"
```

Reservation tests use isolated temporary SQLite databases and fake email transport. They cover eligibility, queue order, expiry, cancellation, return availability, notification records, deduplication, missing email, fulfillment, activity logs, and queries without accessing the development database or sending email.

Run only Priority 7A audit and activity-log tests:

```powershell
dotnet test KicsitLibrary.Tests/KicsitLibrary.Tests.csproj --filter "FullyQualifiedName~AuditComplianceWorkflowTests"
```

Priority 7A tests use isolated temporary SQLite databases and a capturing report-export double. They cover latest-log ordering, filters, details, result limits, snapshot export, audit creation, duplicate rejection, update, status changes, soft deletion, activity logs, report compatibility, and authorization.

Run only Priority 7B inventory and stock-verification tests:

```powershell
dotnet test KicsitLibrary.Tests/KicsitLibrary.Tests.csproj --filter "FullyQualifiedName~InventoryStockVerificationWorkflowTests"
```

Priority 7B tests use isolated temporary SQLite files. They cover inventory validation and lifecycle actions, activity logs, both reports, verification sessions, mismatch rules, completion summaries, explicit reconciliation, and authorization.

Run only Priority 8A backup tests:

```powershell
dotnet test KicsitLibrary.Tests/KicsitLibrary.Tests.csproj --filter "FullyQualifiedName~BackupWorkflowTests"
```

Priority 8A tests create isolated temporary source databases and temporary backup folders. They verify real database files, SQLite integrity checks, SHA-256 checksums, metadata redaction, non-overwrite behavior, ZIP contents, failure history, activity logs, ordering, authorization, and summaries.

Run only Priority 8B restore tests:

```powershell
dotnet test KicsitLibrary.Tests/KicsitLibrary.Tests.csproj --filter "FullyQualifiedName~RestoreWorkflowTests"
```

Priority 8B tests use isolated temporary SQLite databases and folders. They cover real backup preview, integrity and schema validation, SHA-256, required confirmation/reason/safety backup, staged metadata, restore history, authorization, startup replacement, post-restore verification, simulated rollback, logging, compatibility indexes, and backup regression behavior. They never access `KicsitLibrary.db`.

Run only Priority 8C automatic backup scheduler and retention tests:

```powershell
dotnet test KicsitLibrary.Tests/KicsitLibrary.Tests.csproj --filter "FullyQualifiedName~AutomaticBackupSchedulerTests"
```

Priority 8C tests use isolated temporary SQLite databases and temporary backup folders. They cover disabled defaults, authorized real backup creation, overlap prevention, pending-restore skip, status persistence, existing backup-service history, ZIP compression, retention preview, latest/failed/safety/pending protections, history-only cleanup, physical safe-file deletion, authorization blocks, activity logs, and test database isolation.

Run only Priority 8D database and backup ownership tests:

```powershell
dotnet test KicsitLibrary.Tests/KicsitLibrary.Tests.csproj --filter "FullyQualifiedName~DatabaseOwnershipServiceTests"
```

Priority 8D tests use isolated temporary SQLite databases and temporary backup folders. They cover application instance locks, critical operation overlap/timeouts, non-sensitive lease metadata, idempotent release, stale detection/cleanup, cleanup authorization, backup/restore/scheduler/retention lock integration, and no access to `KicsitLibrary.db`.

Run only Priority 9A secure document workflow tests:

```powershell
dotnet test KicsitLibrary.Tests/KicsitLibrary.Tests.csproj --filter "FullyQualifiedName~DocumentWorkflowTests"
```

Priority 9A tests use isolated temporary SQLite databases, temporary source folders, and temporary document storage folders. They cover valid PDF/PNG upload, executable/disallowed/oversized rejection, generated filenames, path traversal neutralization, SHA-256 storage, configured storage root use, activity logs, document-type filtering, unauthorized open blocking, soft delete, restore, missing-file reporting, SOP document reporting, and copy logging. They never access `KicsitLibrary.db` and never open external applications.

Run the Phase 12A/12B deployment smoke script (bypassing execution policy constraints if needed):

```powershell
powershell -ExecutionPolicy Bypass ./scripts/deployment_smoke_test.ps1
```

The script runs `dotnet build KicsitLibrary.slnx`, `dotnet test KicsitLibrary.slnx`, and a local framework-dependent `dotnet publish` for `KicsitLibrary.Desktop` into `artifacts/deployment-smoke/publish`. It does not launch the app, does not create an installer, does not publish to production, and does not intentionally modify the real user database.
It also prints the configured runtime data mode, the release-root guard value, the publish output folder, and a warning that installer elevated permissions and rollback behavior are not covered by the smoke script.

Equivalent publish command used by the script:

```powershell
dotnet publish KicsitLibrary.Desktop/KicsitLibrary.Desktop.csproj -c Release -r win-x64 --self-contained false -o artifacts/deployment-smoke/publish
```


## Manual Document Workflow Verification

1. Sign in as Super Admin or Admin and open **Documents**.
2. Select **Upload Document**, choose a valid `.pdf`, `.docx`, `.xlsx`, `.jpg`, `.jpeg`, or `.png`, and confirm **Validate** succeeds before upload.
3. Upload Library SOP, National Library Rates, Audit Evidence, Visit Evidence, Inventory Document, and General Document examples.
4. Confirm the grid shows title, type, version, original file name, size, uploader, upload date, expiry date, status, and related entity metadata.
5. Apply search, document type, active status, uploader, date range, expired-only, missing-file-only, and related-entity filters.
6. Open details and confirm raw stored paths are not displayed, while SHA-256, stored file name, status, remarks, and related metadata appear.
7. Use **Open Document** on a harmless local test document and confirm an Activity Log entry is written.
8. Use **Copy To** and confirm the selected document is copied without overwriting existing files.
9. Soft-delete with a reason and confirm the row becomes inactive while the physical stored file remains.
10. Restore the document and confirm it returns to active status if the stored file still exists.
11. Temporarily move a stored test file out of storage and confirm the document shows `Missing File`.
12. Sign in as Auditor and confirm only Audit Evidence and Visit Evidence documents can be opened.
13. Sign in as Read Only Viewer and confirm metadata can be viewed only when `VIEW_DOCUMENTS` is granted and open/upload/delete/restore remain blocked.
14. Open **Reports** and run SOP Documents and National Library Rates Documents reports.

## Manual Ownership Verification

1. Sign in as Super Admin or Admin and open **Backup**.
2. Confirm the **Database and Backup Ownership** panel shows application instance, database, backup folder, restore, and scheduler lock statuses.
3. Select **Refresh Ownership Status** and confirm the last ownership message and stale lock count update.
4. Start a long-running backup or restore operation in one application instance, then attempt a competing backup/restore from a second instance and confirm the second operation is blocked or the second instance is restricted according to `SingleInstanceMode`.
5. Confirm blocked critical operations show: `Another Ilm-o-Kutub System operation is already using this database or backup folder.`
6. Confirm **Cleanup Stale Lock Files** is enabled for Admin/Super Admin and removes only expired safe lock files.
7. Sign in as Librarian or Auditor and confirm ownership status is viewable but stale cleanup is disabled.
8. Confirm Activity Logs include lock acquisition/release, timeout, cleanup, and denied cleanup entries where applicable.

## Manual Automatic Backup and Retention Verification

1. Sign in as Super Admin or Admin and open **Backup**.
2. Confirm automatic backup, run on startup, retention, and physical deletion are disabled by default.
3. Enter or keep a backup destination folder and select **Save Scheduler Settings**.
4. Select **Run Backup Now** and confirm a verified backup history row is created.
5. Enable **Create ZIP**, save settings, run again, and confirm the ZIP path appears.
6. Select **Preview Retention** and confirm candidates show whether they can be deleted and why protected rows are kept.
7. Enable retention with a safe age/count policy and keep **Delete physical files** unchecked for the first apply.
8. Select **Apply Retention** and confirm old eligible rows are soft-deleted while physical files remain.
9. Only after a verified manual review, enable **Delete physical files**, preview again, and confirm the warning dialog before applying.
10. Confirm restore safety backups, pending restore files, failed-verification rows, the latest successful backup, and files outside the configured folder are not removed.
11. Confirm Activity Logs include scheduler start/completion/skip/failure and retention deletion/skipped/completed entries.
12. Sign in as Auditor and confirm backup history/status is viewable but configuration, run-now, and retention apply are blocked.

## Manual Restore Verification

1. Sign in as Super Admin or Admin and create a fresh verified backup.
2. Select the completed backup and choose **Restore Selected**.
3. Confirm the preview shows the database path, size, SHA-256 checksum, integrity result, and detected record counts.
4. Enter a reason and type `RESTORE` exactly; keep all safety and verification options enabled.
5. Choose **Stage Restore** and confirm a verified safety backup path and restart-required message are shown.
6. Close and restart the application.
7. Confirm login succeeds, Restore shows a completed startup restore, and Activity Logs contains preview, validation, safety backup, staging, and startup application records.
8. Confirm Librarian/Auditor can view restore history but cannot stage a restore.
9. Confirm an invalid, empty, corrupted, or unrelated SQLite file is rejected.

## Manual Backup Verification

1. Sign in as Super Admin or Admin and open **Backup**.
2. Confirm the default destination is `%USERPROFILE%\Documents\Ilm-o-Kutub Backups`.
3. Enter a reason, keep **Verify after creation** enabled, and select **Create Backup**.
4. Confirm a `.db` file and `.metadata.json` file are created with a timestamped name.
5. Select the history row and use **Verify Selected**; confirm verification is `Passed` and a SHA-256 checksum is shown.
6. Enable **Create ZIP**, create another backup, and confirm the ZIP contains both the database and metadata file while the original `.db` remains.
7. Use **Open Backup Folder** and **View Details**.
8. Confirm `Backup Created` and `Backup Verification Passed` records appear in Activity Logs.
9. Sign in as Auditor or Librarian and confirm history is viewable but creation is blocked.
10. Confirm restore and automatic backup actions are present only in their approved screens, while sync and deployment actions are still absent.

## Manual Inventory and Stock Verification

1. Open **Inventory**, add an item, and verify the quantity validation.
2. Adjust quantities with a reason, then mark units damaged and repaired.
3. Soft-delete with a reason, enable **Include deleted**, and restore the item.
4. Export the Inventory Report and confirm current values.
5. Open **Stock Verification** and start a new session.
6. Verify a copy with a matching status, then verify another with a different status and required remarks.
7. Confirm verification alone does not change `BookCopy`; use **Reconcile Selected** with a reason to apply an explicit status change.
8. Complete the session and export the Stock Verification Report.
9. Confirm inventory and verification actions appear in Activity Logs.

## 3. Manual SMTP Verification

1. Back up the development database before changing settings.
2. Populate `SmtpHost`, `SmtpPort`, `SmtpUseSsl`, `SmtpUser`, `SmtpPassword`, `SmtpFromEmail`, and `SmtpFromName` in `SystemSettings`.
3. Set `EmailNotificationEnabled` to `True`.
4. Open Notifications and select **Validate Email Settings**.
5. Run the overdue check to create pending email records.
6. Use **Send Selected Email** or **Send All Pending Emails**.
7. Confirm the record status, `SentAt`, `LastAttemptAt`, `RetryCount`, and any `FailureReason`.
8. Confirm an activity-log row exists for each attempted delivery.

## 4. Manual Scheduler Verification

1. Back up the development database before changing settings.
2. Confirm `OverdueSchedulerEnabled`, `OverdueSchedulerRunOnStartup`, and `OverdueSchedulerSendPendingEmails` are `False` by default.
3. Open Overdue Reminders and select **Refresh Scheduler Status**.
4. Set `OverdueSchedulerEnabled=True` in `SystemSettings`.
5. Keep `OverdueSchedulerSendPendingEmails=False` for the first run.
6. Select **Run Scheduler Now** and verify notification records are created without email delivery.
7. Run it again and verify same-day records are not duplicated.
8. Confirm last-run, last-success, message, and running-state values update.
9. Configure valid SMTP settings before enabling `OverdueSchedulerSendPendingEmails=True`.
10. Restart the application only after explicitly enabling `OverdueSchedulerRunOnStartup` if a delayed startup run is required.

## 5. Manual Reports Verification

1. Open **Reports** from the sidebar.
2. Search reports by name and confirm the visible report count changes.
3. Confirm all sixteen report cards appear under the seven report categories.
4. Select each Priority 5B report and confirm real database rows appear.
5. Apply text, enum, date, number, and boolean filters where available.
6. Verify invalid date/number ranges show a clear validation message.
7. Confirm **Clear Filters** restores the full preview.
8. Export CSV, Excel, and PDF.
9. Confirm the success message includes the generated file path.
10. Open `%USERPROFILE%\Documents\Ilm-o-Kutub Reports`.
11. Verify the file name includes the report name and timestamp.
12. Confirm repeated exports create unique files instead of overwriting.
13. Confirm wide PDF reports have page metadata, repeated headers, and page numbers.
14. Confirm Excel reports have metadata, filters, frozen headers, and summaries.
15. Confirm a `Report Exported` activity-log record was written.

## 6. Database Initialization
The current development strategy is `EnsureCreatedAsync` only.

Do not run `dotnet ef migrations add InitialCreate` against the current database workflow. A baseline/adoption plan is required first because existing databases may have been created by `EnsureCreatedAsync`.

Priority 4B adds notification columns through a fixed non-destructive SQLite compatibility initializer. This is not a replacement for the pending migration baseline.

Priority 6A uses the same initializer to add faculty/student clearance columns and indexes without deleting or rebuilding existing tables.

Priority 8C stores automatic backup scheduler and retention configuration in existing `SystemSettings` rows. It adds no EF migrations and no destructive schema changes.

Priority 8D stores ownership configuration in existing `SystemSettings` rows and adds no EF migrations or destructive schema changes.

Priority 9A adds `DocumentUploads` compatibility table/columns/indexes only through safe SQLite compatibility SQL. It adds no EF migrations, does not delete existing databases, and does not rename `KicsitLibrary.db`.

Priority 9B does not change the database initialization strategy. Deployment planning documents identify the executable-relative SQLite path as a packaging decision that must be settled before installer rollout.

## Deployment Readiness Documents

Review these before packaging:

```powershell
Get-Content "DEPLOYMENT READINESS AUDIT.md"
Get-Content "PACKAGING STRATEGY.md"
Get-Content "RELEASE TEST PLAN.md"
```

Packaging remains pending. Do not create ClickOnce, MSIX, MSI, Supabase sync, EF migrations, WhatsApp delivery, or final `README.md` until a separate approved task requests it.

## Runtime Data Location Verification

Run only Priority 9C runtime path tests:

```powershell
dotnet test KicsitLibrary.Tests/KicsitLibrary.Tests.csproj --filter "FullyQualifiedName~RuntimePathServiceTests"
```

Priority 9C tests use isolated SQLite databases and temporary folders. They verify release-mode LocalApplicationData defaults, configured runtime roots, path-traversal rejection, folder creation, document-storage fallback, backup fallback, and the guarded database path behavior. They never access `KicsitLibrary.db`.

## 7. Manual Clearance Verification

1. Open **Library Clearance** from the sidebar.
2. Switch between Student Clearance and Faculty and Staff Clearance.
3. Search and select a member, then select **Check Clearance**.
4. Confirm active issues, pending fines, and loss/damage blockers appear with exact reasons.
5. Enter approval remarks and approve an eligible member.
6. Confirm status, date, and activity-log entry are persisted.
7. Generate the certificate and open `%USERPROFILE%\Documents\Ilm-o-Kutub Certificates`.
8. Add a new due after approval and confirm certificate generation is blocked.
9. Enter a revoke reason and revoke clearance.
10. Open Borrowing History and verify issue and clearance history records.

## 8. Manual Reservation Verification

1. Open **Reservations** from the sidebar.
2. Create reservations for active, uncleared student and faculty/staff members.
3. Confirm duplicate reservations, same-title active issues, and pending fines are blocked.
4. Open a title queue and confirm first-come-first-served positions.
5. Return a normally issued copy and confirm the first queue item becomes `Available`.
6. Confirm no book is issued automatically and no email is sent automatically.
7. Open Notifications and confirm in-app and email records exist without duplicates.
8. Confirm a missing member email produces a failed email record with a clear reason.
9. Fulfill the first queue item and confirm an issue record is created, the copy becomes `Issued`, and the reservation stores the accession number with status `Issued`.
10. Cancel or expire an active reservation with a reason and confirm an activity-log row is written.

---

## 9. Run the WPF Desktop Application
Launch the WPF UI:
```powershell
dotnet run --project KicsitLibrary.Desktop
```

## 10. Manual Activity Log and Audit Verification

1. Sign in as Super Admin or Admin and open **Activity Logs**.
2. Confirm the latest records load and action, entity, user, and date filters work.
3. Select a row and open details; verify full message, IDs, source/IP, and parsed metadata.
4. Export the current view to CSV, Excel, and PDF and verify physical files are created.
5. Open **Audit Records** and create an audit with full observations, findings, and suggestions.
6. Confirm duplicate audit numbers are rejected.
7. Edit the record and verify the updated full text persists.
8. Change status with remarks and verify the Activity Log entry.
9. Delete an audit with a reason and confirm it disappears from the active list while an archive row and activity log remain.
10. Sign in as Auditor and confirm records/logs are viewable but audit mutation is blocked.
11. Open **Reports**, run Audit Report, and confirm the audit number and current status appear.

## 11. Manual Branding and Helpful Hints Verification

1. Launch the desktop application and confirm the login title shows **Ilm-o-Kutub System**.
2. Sign in and confirm the window title, sidebar product header, and dashboard welcome state show **Ilm-o-Kutub System**.
3. Confirm the institution name remains separate in the top bar and generated reports.
4. Verify the sidebar uses the concise module labels documented in `CURRENT STATUS.md`.
5. Hover over sidebar items and important actions; confirm practical tooltips appear.
6. Clear **Show Helpful Hints** in the top bar and confirm button/menu tooltips stop appearing immediately.
7. Enable **Show Helpful Hints** and confirm tooltips return.
8. Export an Excel and PDF report and confirm **Ilm-o-Kutub System** appears above the report and institution titles.
9. Generate a clearance certificate and confirm the same product branding.
10. Create a backup and confirm its metadata contains `Ilm-o-Kutub System` and its filename begins `Ilm-o-Kutub_Backup_`.
11. Confirm the application still uses the existing `KicsitLibrary.db` and does not create or delete a renamed database.

---

## 12. SQLite Local Database Inspections
The default SQLite database is named `KicsitLibrary.db`. Relative paths are resolved from `AppContext.BaseDirectory`, normally `KicsitLibrary.Desktop/bin/Debug/net8.0-windows/`.
- Connect to database using SQLite CLI:
  ```bash
  sqlite3 KicsitLibrary.db
  ```
- Query Seeded Accounts:
  ```sql
  SELECT Username, Email, IsActive FROM Users;
  ```
- Inspect Active Library Settings:
  ```sql
  SELECT Key, Value FROM SystemSettings;
  ```
- Check Unpaid Fine Totals:
  ```sql
  SELECT FineRecordNumber, RemainingAmount, PaymentStatus FROM Fines WHERE PaymentStatus = 'Unpaid';
  ```

---

## 13. Pre-Release Security Scan
Before any push to a public repository, run the release security scan:
```powershell
powershell -ExecutionPolicy Bypass -File ./scripts/security_scan.ps1
```
This runs 10 checks:
1. Seeded Demo Password Check (All files except DbSeeder.cs)
2. Public Docs Password Check
3. Generated Repo Logs Check
4. SMTP Password in Non-Service Files
5. SMTP Password in Exports or Docs
6. Private Local Paths in Public Docs
7. Database Files in Repository
8. SQLite Journal Files
9. Private Credentials Template Check
10. Security Documentation Check

Output is fully redacted and findings do not expose plaintext password values.

