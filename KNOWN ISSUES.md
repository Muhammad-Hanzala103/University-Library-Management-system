# Known Issues, Limitations & Incomplete Components

This document outlines modules that are currently implemented or stubbed, listing limitations, missing validations, and runtime constraints.

---

## 1. Placeholder & Stubbed Logic

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

### Navigation View Stubs
- The following screens are registered in the main sidebar menu in `MainWindow.xaml` and mapped in `MainViewModel.cs` but do not have XAML Views implemented. They will load as blank/loading templates:
  - `"Inventory Management"`
  - `"System Settings"`

---

## 2. Incomplete Integrations & Gaps

### Notification Alert Engine
- Manual overdue discovery and idempotent notification-record generation are implemented.
- Email records can be delivered manually from Notification Center through MailKit SMTP.
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
- Scheduler overlap protection uses an in-process semaphore. Separate desktop application processes are not coordinated by an OS mutex or distributed lease.
- Scheduler timeout and shutdown cancellation are cooperative. EF Core and MailKit receive cancellation tokens, but an external provider or operating-system call may not stop instantaneously.
- SQLite busy/locked errors receive three short retries. Other exceptions are persisted and logged without indefinite retry.
- The worker polls settings every 30 seconds while disabled. There is no push-based settings notification.
- The Overdue Reminders screen displays scheduler settings but does not edit them. A full System Settings screen remains pending.
- A stale persisted running flag after an abnormal process termination is cleared when scheduler status is next loaded.
- Deduplication is enforced per issue, notification type, channel, local date, and cooldown query. Multi-process concurrency beyond the unique date key remains a deployment consideration.

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
- Fulfillment is restricted to the first active queue member and reuses circulation eligibility. Multi-process races between separate desktop application instances remain a deployment concern.
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

### Cloud Integration (Supabase Sync)
- Currently, the database provider runs 100% locally on SQLite. There is no background worker that pushes local SQLite transaction records to a remote Supabase Postgres cloud instance.

### System Backups
- Manual verified SQLite backup creation is implemented with the Microsoft.Data.Sqlite online backup API.
- Backup verification runs `PRAGMA integrity_check` and SHA-256 hashing against a separate read-only connection.
- The native online backup call is synchronous internally and cannot be interrupted after it enters SQLite; it runs on a worker thread and observes cancellation before and after the native operation.
- A process-level semaphore prevents overlapping backup operations within one application process. Separate desktop processes are not coordinated.
- Custom destination paths are typed manually; a native folder-picker dialog remains deferred.
- Retention settings are seeded but no automatic file/history deletion is implemented.
- Restore, automatic scheduling, Supabase sync, and deployment remain unimplemented.
- Backup history soft deletion never deletes physical backup files.
- Metadata intentionally excludes all `SystemSettings`, including SMTP credentials.

### Automated Test Coverage
- One hundred forty-eight xUnit tests run against isolated temporary SQLite files.
- Coverage also protects real online backup files, integrity verification, SHA-256 checksums, history, metadata redaction, non-overwrite behavior, ZIP contents, failure handling, logging, ordering, summaries, and authorization.
- The suite does not automate WPF UI interaction, real-time hourly worker delays, multi-process concurrency, migration adoption, a live SMTP server, or visual PDF/Excel layout inspection.
