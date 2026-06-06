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
- **Priority 4D to 8 (Advanced Modules)**: **Pending Implementation**

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

### ViewModels (`KicsitLibrary.Desktop/ViewModels/`)
- `MainViewModel.cs`: Shell navigation controller.
- `LoginViewModel.cs`: Core authentication viewmodel.
- `DashboardViewModel.cs`: Basic dashboard summaries.
- `BookCatalogViewModel.cs` / `BookFormViewModel.cs` / `CopiesViewModel.cs` / `AuthorViewModel.cs` / `PublisherViewModel.cs`: Catalog module.
- `StudentsManagementViewModel.cs` / `StudentFormViewModel.cs` / `FacultyStaffManagementViewModel.cs` / `FacultyStaffFormViewModel.cs` / `ConsumerProfileViewModel.cs` / `VisitRecordsViewModel.cs` / `VisitRecordFormViewModel.cs`: Consumer module.
- `IssueMaterialViewModel.cs` / `ReceiveMaterialViewModel.cs` / `FinesManagementViewModel.cs`: Circulation module.
- `OverdueRemindersViewModel.cs` / `NotificationCenterViewModel.cs`: Manual overdue and notification-record operations.

### Views (`KicsitLibrary.Desktop/Views/` & Root)
- `MainWindow.xaml` / `MainWindow.xaml.cs`: Primary shell window.
- `LoginWindow.xaml` / `LoginWindow.xaml.cs`: Hashed credentials authorization dialog.
- `DashboardView.xaml`: Stats and indicators.
- `BookCatalogView.xaml` / `BookFormWindow.xaml` / `CopiesWindow.xaml` / `AuthorWindow.xaml` / `PublisherWindow.xaml`
- `StudentsManagementView.xaml` / `StudentFormWindow.xaml` / `FacultyStaffManagementView.xaml` / `FacultyStaffFormWindow.xaml` / `ConsumerProfileWindow.xaml` / `VisitRecordsView.xaml` / `VisitRecordWindow.xaml`
- `IssueMaterialView.xaml` / `ReceiveMaterialView.xaml` / `FinesManagementView.xaml`
- `OverdueRemindersView.xaml` / `NotificationCenterView.xaml`

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

---

## 3. Pending Components

- **Views & ViewModels**:
  - `AuditRecordsView.xaml` & `AuditRecordsViewModel.cs` (Mapped but not implemented)
  - `InventoryManagementView.xaml` & `InventoryManagementViewModel.cs` (Mapped but not implemented)
  - `ReportsView.xaml` & `ReportsViewModel.cs` (Mapped but not implemented)
  - `SystemSettingsView.xaml` & `SystemSettingsViewModel.cs` (Mapped but not implemented)
- **Services**:
- A hosted overdue scheduler. Manual SMTP delivery is complete.
  - `IReportService`: Custom reports generation and formatting.
  - `IClearanceService`: Student/Faculty final departure settlement database routines.
  - `IBackupSyncService`: Local backup scripts and Supabase sync logic.
