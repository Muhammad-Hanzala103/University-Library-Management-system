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

## Priority 4B: Overdue Engine & Notifications
- **Goal**: Implement automatic background scanning for overdue books and notify users.
- [ ] Create `INotificationService.cs` in `KicsitLibrary.Core/Interfaces/`.
- [ ] Create `NotificationService.cs` in `KicsitLibrary.Services/`.
- [ ] Implement an automated schedule checker that queries active `IssueRecords` where `DateTime.UtcNow > ExpectedReturnDate` and status is not returned.
- [ ] Add `NotificationRecord` entities for trackable logs.
- [ ] Implement email client dispatcher (using SMTP configuration resolved from `SystemSettings`).
- [ ] Create `OverdueRemindersViewModel.cs` under `KicsitLibrary.Desktop/ViewModels/`.
- [ ] Create `OverdueRemindersView.xaml` under `KicsitLibrary.Desktop/Views/` to list all late items, show calculations, and trigger reminders.

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
