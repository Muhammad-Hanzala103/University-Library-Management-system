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

## Priority 5A: Reports & Export Foundation
- **Goal**: Add reusable data-first reporting and the first five production reports.
- [x] Define report contracts, data rows, filters, results, and export requests.
- [x] Implement Catalog, Issued Books, Overdue Books, Fine, and Notification providers.
- [x] Implement physical CSV, Excel, and PDF exports.
- [x] Add safe Documents-folder storage and non-overwriting file names.
- [x] Log export success and failure.
- [x] Add Reports dashboard, report-specific filters, preview, empty state, and export actions.
- [x] Wire Reports & Analytics navigation and DI.
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
- [ ] Define reservation state-transition rules and authorization boundaries.
- [ ] Implement queue ordering, expiry processing, cancellation, and fulfillment.
- [ ] Add reservation management MVVM screens and isolated SQLite tests.
- [ ] Keep Priority 6A clearance rules backward compatible.

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

---

## Final Release Documentation
- [ ] At final release, generate a complete professional `README.md` in the repository root so GitHub displays the project overview on the repository front page.
- [ ] Include the project title, project overview, key features, technology stack, architecture, screenshots section placeholder, installation guide, database setup, default login accounts, build commands, test commands, release notes, known limitations, future improvements, contributors, and license placeholder.

The project is still under active development. The final `README.md` will be generated after all main modules, testing, deployment, and release packaging are complete.
