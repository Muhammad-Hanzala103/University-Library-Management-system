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
- **Remaining limitation**: SQLite cannot add the new `IssueRecordId` foreign-key constraint to an existing table without a table rebuild. Fresh databases receive the EF-configured constraint; upgraded development databases rely on service validation until migrations are adopted.

### Navigation View Stubs
- The following screens are registered in the main sidebar menu in `MainWindow.xaml` and mapped in `MainViewModel.cs` but do not have XAML Views implemented. They will load as blank/loading templates:
  - `"Audit Records"`
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
- Reservation reporting reflects existing records, but the reservation lifecycle UI/workflow remains partial.
- Visit status is derived as `Pending Follow Up` when a follow-up date exists and no action is recorded; `VisitRecord` has no persisted status field.
- Stock Verification `Actual Status` currently equals the database status, and physical verification remarks state that on-shelf verification is pending.
- Student Clearance reporting calculates outstanding books/fines independently; the Priority 6 clearance service and approval workflow remain pending.

### Cloud Integration (Supabase Sync)
- Currently, the database provider runs 100% locally on SQLite. There is no background worker that pushes local SQLite transaction records to a remote Supabase Postgres cloud instance.

### System Backups
- Database backup dump scripts, file replication utilities, and SQL recovery mechanisms are pending implementation.

### Automated Test Coverage
- Sixty-eight xUnit tests run against isolated temporary SQLite files.
- Coverage protects circulation, overdue and notification behavior plus all sixteen report definitions, advanced providers, filters, exporter selection, file naming, non-overwrite behavior, and physical CSV/XLSX/PDF creation.
- There is no clearance service or clearance eligibility helper, so the requested "active issue blocks student clearance" test is pending Priority 6.
- The suite does not automate WPF UI interaction, real-time hourly worker delays, multi-process concurrency, migration adoption, a live SMTP server, or visual PDF/Excel layout inspection.
