# Next Development Priorities (Phases 4 - 8)

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
- [x] Add Overdue Reminders and Notification Center MVVM views.
- [x] Wire navigation, DI, dashboard overdue counting, settings, and audit logs.
- [x] Add ten Priority 4B tests; all nineteen tests pass.

## Priority 4C: Manual Email Delivery
- **Goal**: Add explicit operator-triggered SMTP delivery for existing pending email records.
- [x] Define SMTP configuration without committing credentials.
- [x] Add an email delivery abstraction and bounded retry handling.
- [x] Update pending/failed/sent records with attempt timestamps and failure details.
- [x] Add integration tests with a fake email transport.
- [x] Add Notification Center actions for selected, retry, batch, and settings validation.
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
- [ ] Add cross-process ownership protection before supporting multiple simultaneous desktop application instances.

---

## Priority 5: Reports & Exports
- **Goal**: Implement structured printouts and document compilation for library files.
- [ ] Create `IReportService.cs` in `KicsitLibrary.Core/Interfaces/`.
- [ ] Create `ReportService.cs` in `KicsitLibrary.Services/` (using iText7 and ClosedXML).
- [ ] Implement PDF layout generator helper classes (generating catalog summaries, consumer checkouts history, and active balance sheets).
- [ ] Implement Excel spreadsheet exports for Catalog lists and Fine registers.
- [ ] Implement CSV text file outputs.
- [ ] Create `ReportsViewModel.cs` and `ReportsView.xaml` layout panel (allowing date-range filtering, category selections, and output type picks).

---

## Priority 6: Clearance System
- **Goal**: Provide university clearance checkouts for graduating students and departing faculty.
- [ ] Create `IClearanceService.cs` in `KicsitLibrary.Core/Interfaces/`.
- [ ] Create `ClearanceService.cs` in `KicsitLibrary.Services/`.
- [ ] Implement verification logic: check for outstanding books checkouts, unpaid fine balances, and active reservations.
- [ ] Write database update routines to set `ClearanceStatus` to `Cleared`, archive active borrower card accounts, and log audited transitions.
- [ ] Create view and viewmodel files for Clearance requests validation.

---

## Priority 7: Auditing, Compliance & Inventory
- **Goal**: Implement internal compliance records and catalog shelf inspection tools.
- [ ] Create `InventoryManagementViewModel.cs` and `InventoryManagementView.xaml` to track and reconcile physical stock levels.
- [ ] Create `AuditRecordsViewModel.cs` and `AuditRecordsView.xaml` to display the `ActivityLog` table entries.
- [ ] Implement visual search filters for activity logs by category, time, user, and level.
- [ ] Add support for listing new book copy arrivals and catalog compliance records.

---

## Priority 8: Backup, Sync & Deployment
- **Goal**: Protect data integrity via local backup scripts, cloud replication, and packaging.
- [ ] Create backup services executing SQL raw dumps (`.sql` scripts) or file-copy tasks of the SQLite `.db` database.
- [ ] Implement database restore logic with automatic verification checks.
- [ ] Integrate Supabase Sync: push local updates to Supabase cloud database to support remote sync backups.
- [ ] Configure `appsettings.json` encryption routines for sensitive credentials.
- [ ] Package the solution: configure ClickOnce deployment or an MSI installer package.
