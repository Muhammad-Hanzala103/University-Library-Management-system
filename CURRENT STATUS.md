# Current Project Status

This document catalogs all implemented and pending files, services, entities, ViewModels, and views inside the University Library Management System.

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
- **Priority 7 to 8 (Advanced Modules)**: **Pending Implementation**

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
- Export paths default to the current user's Documents folder under `KICSIT Library Reports`.
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
- Clearance certificates are real PDF files under `Documents\KICSIT Library Certificates` by default and are revalidated before export.
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

### Navigation & Routes Wired
- `"Dashboard"` -> `DashboardViewModel`
- `"Book Catalog"` -> `BookCatalogViewModel`
- `"Issue Material"` -> `IssueMaterialViewModel`
- `"Receive Material"` -> `ReceiveMaterialViewModel`
- `"Fines Management"` -> `FinesManagementViewModel`
- `"Students Management"` -> `StudentsManagementViewModel`
- `"Faculty & Staff"` -> `FacultyStaffManagementViewModel`
- `"Visit Records"` -> `VisitRecordsViewModel`
- `"Overdue Reminders"` -> `OverdueRemindersViewModel`
- `"Notification Center"` -> `NotificationCenterViewModel`
- `"Reports & Analytics"` -> `ReportsDashboardViewModel`
- `"Clearance"` -> `ClearanceDashboardViewModel`
- `"Reservations"` -> `ReservationManagementViewModel`

---

## 3. Pending Components

- **Views & ViewModels**:
  - `AuditRecordsView.xaml` & `AuditRecordsViewModel.cs` (Mapped but not implemented)
  - `InventoryManagementView.xaml` & `InventoryManagementViewModel.cs` (Mapped but not implemented)
  - `SystemSettingsView.xaml` & `SystemSettingsViewModel.cs` (Mapped but not implemented)
- **Services**:
  - `IBackupSyncService`: Local backup scripts and Supabase sync logic.
- **Final Release Documentation**:
  - Generate a complete professional repository-root `README.md` at final release so GitHub displays the project overview on the repository front page.
  - The final README must include the project title, overview, key features, technology stack, architecture, screenshots placeholder, installation guide, database setup, default login accounts, build and test commands, release notes, known limitations, future improvements, contributors, and license placeholder.
  - The project is still under active development, so the final `README.md` will be generated only after all main modules, testing, deployment, and release packaging are complete.
