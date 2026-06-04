# Current Project Status

This document catalogs all implemented and pending files, services, entities, ViewModels, and views inside the University Library Management System.

---

## 1. Summary of Progress
- **Phase A (Codebase Audit & Core Refactoring)**: **100% Completed**
- **Priority 1 (Library Catalog Management)**: **100% Completed**
- **Priority 2 (Consumer Management)**: **100% Completed**
- **Priority 3 (Circulation System)**: **100% Completed**
- **Priority 4 to 8 (Advanced Modules)**: **Pending Implementation**

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

### ViewModels (`KicsitLibrary.Desktop/ViewModels/`)
- `MainViewModel.cs`: Shell navigation controller.
- `LoginViewModel.cs`: Core authentication viewmodel.
- `DashboardViewModel.cs`: Basic dashboard summaries.
- `BookCatalogViewModel.cs` / `BookFormViewModel.cs` / `CopiesViewModel.cs` / `AuthorViewModel.cs` / `PublisherViewModel.cs`: Catalog module.
- `StudentsManagementViewModel.cs` / `StudentFormViewModel.cs` / `FacultyStaffManagementViewModel.cs` / `FacultyStaffFormViewModel.cs` / `ConsumerProfileViewModel.cs` / `VisitRecordsViewModel.cs` / `VisitRecordFormViewModel.cs`: Consumer module.
- `IssueMaterialViewModel.cs` / `ReceiveMaterialViewModel.cs` / `FinesManagementViewModel.cs`: Circulation module.

### Views (`KicsitLibrary.Desktop/Views/` & Root)
- `MainWindow.xaml` / `MainWindow.xaml.cs`: Primary shell window.
- `LoginWindow.xaml` / `LoginWindow.xaml.cs`: Hashed credentials authorization dialog.
- `DashboardView.xaml`: Stats and indicators.
- `BookCatalogView.xaml` / `BookFormWindow.xaml` / `CopiesWindow.xaml` / `AuthorWindow.xaml` / `PublisherWindow.xaml`
- `StudentsManagementView.xaml` / `StudentFormWindow.xaml` / `FacultyStaffManagementView.xaml` / `FacultyStaffFormWindow.xaml` / `ConsumerProfileWindow.xaml` / `VisitRecordsView.xaml` / `VisitRecordWindow.xaml`
- `IssueMaterialView.xaml` / `ReceiveMaterialView.xaml` / `FinesManagementView.xaml`

### Navigation & Routes Wired
- `"Dashboard"` -> `DashboardViewModel`
- `"Book Catalog"` -> `BookCatalogViewModel`
- `"Issue Material"` -> `IssueMaterialViewModel`
- `"Receive Material"` -> `ReceiveMaterialViewModel`
- `"Fines Management"` -> `FinesManagementViewModel`
- `"Students Management"` -> `StudentsManagementViewModel`
- `"Faculty & Staff"` -> `FacultyStaffManagementViewModel`
- `"Visit Records"` -> `VisitRecordsViewModel`

---

## 3. Pending Components

- **Views & ViewModels**:
  - `OverdueRemindersView.xaml` & `OverdueRemindersViewModel.cs` (Mapped but not implemented)
  - `AuditRecordsView.xaml` & `AuditRecordsViewModel.cs` (Mapped but not implemented)
  - `InventoryManagementView.xaml` & `InventoryManagementViewModel.cs` (Mapped but not implemented)
  - `ReportsView.xaml` & `ReportsViewModel.cs` (Mapped but not implemented)
  - `SystemSettingsView.xaml` & `SystemSettingsViewModel.cs` (Mapped but not implemented)
- **Services**:
  - `INotificationService`: Alerts and email notification engines.
  - `IReportService`: Custom reports generation and formatting.
  - `IClearanceService`: Student/Faculty final departure settlement database routines.
  - `IBackupSyncService`: Local backup scripts and Supabase sync logic.
