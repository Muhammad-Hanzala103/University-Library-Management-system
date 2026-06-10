# Known Issues, Limitations & Incomplete Components

This document outlines modules that are currently implemented or stubbed, listing limitations, missing validations, and runtime constraints.

---

## 1. Placeholder & Stubbed Logic

### Branding and UI Refinement Limitations
- The visible product name is **Ilm-o-Kutub System**, but internal `KicsitLibrary.*` namespaces, projects, executable assembly name, solution name, and the `KicsitLibrary.db` file remain unchanged to avoid breaking code, restore compatibility, or user data.
- The **Show Helpful Hints** preference is session-only. It defaults to enabled on each application start and is not written to `SystemSettings`.
- Tooltips are wired through shared button styles and practical per-action text, but WPF hover behavior and visual palette rendering are not covered by automated UI tests.
- Existing customized report and SMTP settings are preserved. Startup changes only exact legacy default branding values.
- Legacy product-name literals remain only in the exact-value upgrade map and regression assertions needed to detect or replace old defaults; they are not displayed as current branding.
- GitHub repository rename to `Ilm-o-Kutub-System` is a manual owner action and has not been performed.
- Final `README.md` generation remains deferred until all modules, testing, deployment, and release packaging are complete.

### Database Initialization Strategy
- **Decision**: Priority 4A uses `EnsureCreatedAsync` only. `MigrateAsync` is not called.
- **Reason**: Existing development databases may have been created by `EnsureCreatedAsync`; adding a normal initial migration could conflict with those schemas.
- **Path**: `Data Source=KicsitLibrary.db` resolves to `KicsitLibrary.Desktop/bin/<Configuration>/net8.0-windows/KicsitLibrary.db` when running from build output.
- **Limitation**: `EnsureCreatedAsync` does not evolve an existing schema. A reviewed baseline/adoption plan remains required before staging or production migrations.
- **Data safety**: Startup never deletes, recreates, or automatically relocates an existing database. Databases previously created under a different working directory must be identified and moved only through an explicit backup/restore procedure.
- **Failure behavior**: Database initialization and seeding failures are fatal and stop startup instead of opening the application in a partial state.
- **Priority 4B compatibility**: Startup applies fixed, additive SQLite `ALTER TABLE` statements for new notification columns and creates indexes if missing. It does not delete or rebuild tables.
- **Priority 6A compatibility**: Startup adds nullable/defaulted clearance columns to `Students` and `FacultyStaff` and creates clearance-status indexes. Existing rows default to `NotCleared`; no table rebuild or data deletion occurs.
- **Remaining limitation**: SQLite cannot add the new `IssueRecordId` foreign-key constraint to an existing table without a table rebuild. Fresh databases receive the EF-configured constraint; upgraded development databases rely on service validation until migrations are adopted.


### Deployment Preparation Limitations
- Priority 9B is an audit and planning phase only. No installer, ClickOnce package, MSIX package, production publish, Supabase sync, EF migrations, WhatsApp delivery, final README, repository rename, namespace rename, or database rename was performed.
- The default SQLite connection string is still `Data Source=KicsitLibrary.db`, resolved from `AppContext.BaseDirectory`. This is predictable but not installer-ready if the install folder is read-only.
- A release database location decision is still required before broad installer deployment. Portable/internal pilot deployments may keep the executable-relative database only with explicit data-preservation instructions.
- EF migrations remain absent. Existing databases rely on `EnsureCreatedAsync` plus additive SQLite compatibility SQL, which is not a full production migration strategy.
- Default seeded user accounts and passwords exist for development and first-run access. Any real deployment must require password reset or documented credential change.
- SMTP passwords are not seeded with real values, but configured SMTP credentials are still stored in local `SystemSettings` without encryption.
- `LocalBackupFolder` still seeds the legacy value `C:\KicsitLibraryBackup`; current Priority 8A+ backup workflows use `BackupDefaultFolder`/automatic backup settings and default to the user's Documents folder when empty. The legacy setting should be removed or migrated in a future cleanup.
- The repository previously had no `.gitignore`; many `bin`, `obj`, `.vs`, and local database artifacts were already tracked. Priority 9B added `.gitignore` for future artifacts. Priority 9E successfully untracked 1082 generated artifacts (14 .vs/, 407 bin/, 661 obj/ files) using non-destructive `git rm --cached` commands. All local files remain preserved on disk. Future generated artifacts will not be tracked.
- Release signing certificate, installer tooling, update strategy, rollback strategy, and support policy remain pending.
- WPF UI automation is still absent; `RELEASE TEST PLAN.md` must be executed manually before packaging.

### Runtime Data Location Limitations
- Priority 9C adds a runtime path service and release data-location strategy, but startup still uses the existing executable-relative SQLite connection by default.
- `UseReleaseDataRoot=False` and `RuntimeStorageMode=Development` preserve current development behavior. This avoids creating a second empty release database under AppData without an approved migration plan.
- Database relocation to `%LOCALAPPDATA%\Ilm-o-Kutub System` is implemented through `DatabaseRelocationService` and `IRuntimePathService`, but it is still explicit and not wired into automatic startup relocation. The release root is enabled only after explicit successful relocation verification.
- Document storage and backup default folders now use runtime path resolution only when their explicit settings are empty. Existing configured folders still take precedence.
- Restore staging uses runtime staging only when the runtime database path matches the active configured SQLite database path; otherwise it stays beside the database so startup can find pending restore metadata.
- Report exports, clearance certificates, ownership lock files, logs, and temp folder usage are not fully integrated with `IRuntimePathService` yet.
- Source-control cleanup has been completed in Priority 9E. Generated artifacts have been untracked from git using non-destructive commands. All local files remain preserved on disk.

---

## 2. Incomplete Integrations & Gaps

### Notification Alert Engine
- Manual overdue discovery and idempotent notification-record generation are implemented.
- Email records can be delivered manually from Notifications through MailKit SMTP.
- SMTP delivery is disabled by default. Empty development placeholders are seeded for host, user, password, and sender address.
- SMTP credentials are stored in the local `SystemSettings` table. They are not committed in source or written to activity logs, but encrypted secret storage is not implemented yet.
- Operators must populate `SmtpHost`, `SmtpUser`, `SmtpPassword`, and `SmtpFromEmail`, then enable `EmailNotificationEnabled`.
- TLS mode is derived from `SmtpUseSsl`: port 465 uses implicit SSL, other SSL-enabled ports use STARTTLS, and disabling SSL uses an unencrypted connection.
- Delivery is manual only; pending records are not sent at startup or by the overdue check.
- Retry limits are bounded by `MaxNotificationRetryCount`. A delivery attempt increments `RetryCount`; validation blocks do not.
- Provider-specific SMTP behavior and real-server interoperability are not covered by automated tests.
- `InApp` records are persisted but there is no per-user popup/badge delivery mechanism yet.
- WhatsApp remains a disabled placeholder.
- A hosted overdue scheduler is implemented but disabled by default.
- Startup runs and automatic pending-email delivery are independently disabled by default.
- Scheduler overlap protection uses both an in-process semaphore and Priority 8D cross-process ownership locks for configured SQLite databases.
- Scheduler timeout and shutdown cancellation are cooperative. EF Core and MailKit receive cancellation tokens, but an external provider or operating-system call may not stop instantaneously.
- SQLite busy/locked errors receive three short retries. Other exceptions are persisted and logged without indefinite retry.
- The worker polls settings every 30 seconds while disabled. There is no push-based settings notification.
- The Overdue Reminders screen displays scheduler settings but does not edit them. A full Settings screen remains pending.
- A stale persisted running flag after an abnormal process termination is cleared when scheduler status is next loaded.
- Deduplication is enforced per issue, notification type, channel, local date, and cooldown query. Priority 8D adds local database ownership locks, but distributed/cloud multi-client contention remains a deployment consideration.

### Reports & Exports
- Sixteen reports are implemented: the five Priority 5A reports plus Student Clearance, Student Borrowing History, Faculty Staff Borrowing History, Reservation, Lost And Damaged Books, Deleted Books Archive, Visit Detail, Audit, Inventory, New Arrivals, and Stock Verification.
- CSV, Excel, and PDF exports create real files. Excel uses ClosedXML and does not require Microsoft Excel.
- The PDF writer intentionally uses built-in PDF primitives to avoid adding a commercial/AGPL reporting dependency. It supports landscape pages, repeating metadata/table headers, width-bounded columns, multiple pages, page numbers, filters, and summaries, but not advanced fonts, logos, or charts.
- PDF text is currently normalized to basic ASCII. Non-ASCII names may appear as `?` until embedded Unicode fonts are added.
- The UI exports to the default Documents folder. A user-selected save dialog remains deferred.
- Providers currently materialize report rows before applying some complex text filters. Very large databases may require paging or server-side filter optimization.
- Report previews use dynamic auto-generated DataGrid columns and are not covered by WPF UI automation.
- Reservation reporting now reflects completed lifecycle states and active queue positions.
- Visit status is derived as `Pending Follow Up` when a follow-up date exists and no action is recorded; `VisitRecord` has no persisted status field.
- Stock Verification reporting uses the latest persisted verification session. Before the first session, rows remain `Unverified`.
- Student Clearance reporting now includes the same active-issue, pending-fine, and loss/damage counts used by the clearance service.

### Clearance Workflow
- Clearance checks use current `IssueRecord`, `Fine`, and `BookCopy` state; they do not complete or cancel reservations.
- A loss/damage case is considered unresolved when the copy has a loss/damage/missing/repair status and the related issue is active or has an unpaid/partial fine.
- Approval history is stored in structured `ActivityLog` details rather than a separate clearance-history table.
- Certificate PDFs inherit the existing basic-ASCII limitation; non-ASCII names may render as `?`.
- Faculty/staff clearance fields are additive and default existing members to `NotCleared`.
- Approval does not deactivate member accounts automatically. Account archival remains an explicit policy decision.

### Reservation Workflow
- Reservation lifecycle states are `Pending`, `Available`, `Issued`, `Cancelled`, and `Expired`; `Issued` is the persisted fulfilled state because the existing enum has no separate `Fulfilled` value.
- Reservation eligibility has no configured maximum active-reservation count. The service reports the current count, but no limit is enforced until the library approves a policy setting.
- Expired reservations are processed when reservation management refreshes or when the explicit expiry service method is invoked. No new scheduler or background engine was added in Priority 6B.
- A normal return is committed before the reservation availability hook runs. This protects completed circulation transactions; if the subsequent availability update fails, the operator receives a clear message and can retry from Reservation Management.
- Availability creates records only. Email remains subject to the existing manual SMTP workflow, and in-app records still have no popup or badge delivery mechanism.
- Reservation notification deduplication uses the existing unique `DeduplicationKey` field with reservation-specific keys; no new database columns or migration were required.
- Fulfillment is restricted to the first active queue member and reuses circulation eligibility. Priority 8D protects database-critical utility operations, but reservation-specific multi-process contention tests remain deferred.
- No WPF UI automation covers the new reservation windows; service behavior is covered by isolated SQLite integration tests.

### Activity Logs and Audit Records
- Existing `ActivityLog` rows do not have dedicated entity, entity-ID, severity, outcome, or source columns. Priority 7A derives entity metadata from established `key=value` details and derives failure state from action/detail text.
- Older unstructured log messages remain viewable and searchable but may show blank entity fields.
- The default browser result is capped at the latest 500 matching rows; the service permits an explicit maximum of 2,000.
- Activity-log deletion is not exposed in the normal UI. The protected service operation is restricted to Super Admin/Admin, soft-deletes matching rows, and writes a summary archive.
- Admin access uses a documented role-name fallback because the seeded Admin role does not currently include `VIEW_AUDITS` or `MANAGE_AUDITS`.
- Librarian management follows `MANAGE_AUDITS`; Auditor is view-only; other roles require `VIEW_AUDITS`.
- `AuditFile` does not inherit `EntityBase` and has no soft-delete metadata. Attachments are therefore read-only in Priority 7A; add/remove actions were not implemented because they could not satisfy the no-permanent-delete rule safely.
- Audit-number uniqueness is enforced by the service. A unique database index was not added because existing `EnsureCreated` databases may contain duplicates and no migration/adoption process exists.
- No database compatibility columns or EF migrations were required for Priority 7A.
- WPF UI automation and multi-process mutation concurrency are not covered.

### Inventory and Stock Verification
- Stock-verification sessions and entries are additive `CREATE TABLE IF NOT EXISTS` compatibility tables. EF migrations remain deferred.
- Verification never changes `BookCopy` automatically; reconciliation requires an explicit action and reason.
- Compatibility SQL adds unique indexes but does not add foreign-key constraints to existing databases, avoiding a destructive table rebuild.
- Inventory document paths are displayed as existing metadata only. Upload/remove was not implemented because deep signature validation and managed application-data storage are a separate workflow.
- Dashboard inventory totals remain unchanged; mismatch and unverified cards were not added to avoid expanding the dashboard scope.
- WPF UI automation and cross-process inventory/verification write contention are not covered.

### Secure Document Upload Workflow
- Priority 9A stores administrative documents in managed local storage and keeps `DocumentUploads` records in SQLite. It does not upload documents to cloud storage.
- Physical file deletion is intentionally deferred. Soft delete marks the database record inactive/deleted and leaves the stored file on disk.
- Document storage settings are seeded in `SystemSettings`, but there is no operator-facing Settings screen yet for `DocumentStorageRoot`, `DocumentMaxFileSizeMb`, `DocumentAllowPhysicalDelete`, or `DocumentAllowedExtensions`.
- The copy action uses a standard WPF save dialog to choose a destination path and then copies to that destination folder. A native folder-picker dialog remains deferred.
- Basic signature checks are implemented for PDF, PNG, JPG/JPEG, DOCX, and XLSX. They reduce obvious mismatch risk but are not full malware scanning or deep file-content validation.
- Stored document paths are not exposed in the UI; details show generated stored file names and metadata only.
- Audit, Visit, and Inventory links are stored through `RelatedEntityType` and `RelatedEntityId`. Inline related-document panels inside those existing detail screens remain deferred to avoid rewriting stable modules.
- `AuditFile` and `VisitFile` entities are preserved unchanged; Priority 9A does not migrate or replace those attachment records.
- SOP Documents and National Library Rates Documents reports are added. No broader report redesign was done.
- WPF UI automation does not cover the new document screens; service and report behavior is covered by isolated SQLite/temp-folder tests.

### Cloud Integration (Supabase Sync)
- Currently, the database provider runs 100% locally on SQLite. There is no background worker that pushes local SQLite transaction records to a remote Supabase Postgres cloud instance.

### System Backups
- Manual verified SQLite backup creation is implemented with the Microsoft.Data.Sqlite online backup API.
- Backup verification runs `PRAGMA integrity_check` and SHA-256 hashing against a separate read-only connection.
- The native online backup call is synchronous internally and cannot be interrupted after it enters SQLite; it runs on a worker thread and observes cancellation before and after the native operation.
- A process-level semaphore and Priority 8D ownership lease protect backup operations for the configured local SQLite database.
- Custom destination paths are typed manually; a native folder-picker dialog remains deferred.
- Automatic backup scheduling is implemented but disabled by default. Startup runs, retention, and physical file deletion are also disabled by default.
- The automatic scheduler runs only while the desktop application is running after database initialization and login. It is not a Windows Service and does not run while the application is closed.
- Automatic backup uses the existing real backup service, so it inherits the same SQLite online backup, verification, ZIP compression, activity logging, and authorization rules as manual backup.
- Automatic backup is skipped while pending restore metadata exists.
- Retention preview and apply are implemented. History cleanup is soft-delete only unless `AutomaticBackupDeletePhysicalFiles=True`.
- Physical retention deletion is intentionally conservative: it protects the live database, latest successful backup, failed-verification backups, restore safety backups, emergency restore files, pending restore files, unsupported extensions, files outside the configured backup folder, and detectable symbolic/reparse paths.
- Physical file deletion and database soft-delete updates cannot be made truly atomic together because the file system and SQLite transaction are separate resources. The service validates first, logs outcomes, and keeps history soft-delete as the durable database action.
- Retention physical deletion requires a Priority 8D critical operation lock before deleting files.
- Verified local restore is implemented as a staged, restart-required operation. The active database is never replaced while application DbContexts are running.
- Restore validation checks file size, SQLite integrity, SHA-256, and required application tables, but it does not prove semantic correctness of every row.
- Startup creates an emergency copy and uses the mandatory safety backup for rollback protection. Staged, emergency, and safety files are retained; automatic cleanup is intentionally not implemented.
- Only one pending restore is supported per configured database. Priority 8D ownership protection blocks competing restore/backup critical operations for the same local SQLite database.
- Restore accepts `.db` files. ZIP extraction is not implemented; use the retained database file produced by Backup.
- Restore paths are selected from backup history or typed manually; a native file picker remains deferred.
- A critical replacement plus rollback failure stops startup and preserves pending metadata for manual recovery.
- Supabase sync and deployment remain unimplemented.
- Manual backup history soft deletion never deletes physical backup files; automatic retention deletes physical files only when explicitly enabled and safety validation passes.
- Metadata intentionally excludes all `SystemSettings`, including SMTP credentials.

### Automated Test Coverage
- Two hundred twenty xUnit tests run against isolated temporary SQLite files and directories.
- Branding tests cover the centralized product name, key UI files, and the default/on-off hint preference behavior.
- Coverage also protects real online backup and staged restore files, automatic backup scheduling, retention safety rules, ownership lock acquisition/release, stale-lock cleanup, cross-service backup/restore/scheduler/retention lock failures, secure document validation/upload/storage/logging/filtering/authorization/soft-delete/restore/reporting, integrity/schema verification, SHA-256 checksums, safety backups, startup replacement, rollback, history, metadata redaction, non-overwrite behavior, ZIP contents, failure handling, logging, ordering, summaries, and authorization.
- The suite does not automate WPF UI interaction, real-time hourly worker delays, deployment-scale multi-process stress testing, migration adoption, a live SMTP server, malware scanning, or visual PDF/Excel layout inspection.

### Database and Backup Ownership
- Priority 8D uses an OS named mutex for the application instance and per-domain lease files for database, backup, restore, and scheduler critical operations.
- Lease cleanup is intentionally conservative. It deletes only expired safe lock files or old unreadable files that can be opened exclusively.
- Active lock files are never deleted by cleanup, even if they look old.
- Lease files contain non-sensitive operational metadata only. They intentionally exclude `SystemSettings`, SMTP passwords, database passwords, and connection strings.
- Ownership settings are stored in `SystemSettings`; there is no dedicated Settings screen yet to edit timeout, retention, read-only second instance, or startup cleanup policies.
- Cross-process tests simulate competing ownership services against isolated local SQLite files. Full installer/runtime multi-instance stress testing remains a manual/deployment task.
