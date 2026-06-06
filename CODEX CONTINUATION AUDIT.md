# Codex Continuation Audit

## Priority 4B Completion Update

Completion date: 2026-06-06

Priority 4B is complete:

- Added deterministic active-issue overdue queries using local due-date boundaries.
- Added `OverdueItem`, `IOverdueService`, and `OverdueService`.
- Added `INotificationService` and record-only `NotificationService`.
- Notification records now persist issue linkage, recipient snapshots, retry/read metadata, attempt timestamps, and deduplication keys.
- Manual processing creates `InApp` and `Email` records idempotently.
- Email remains pending or failed; no SMTP transport exists.
- Missing email, duplicate skips, starts, completions, retries, reads, and errors are audited.
- Added Overdue Reminders and Notification Center views, ViewModels, DI registrations, and navigation.
- Dashboard overdue totals now use active overdue issue rows.
- Added fixed additive SQLite compatibility updates without deleting or rebuilding user tables.
- Added ten tests; all nineteen tests pass.

Verification:

```powershell
dotnet build KicsitLibrary.slnx
dotnet test KicsitLibrary.slnx
```

Result: build succeeded with 0 warnings and 0 errors; 19 tests passed.

Deferred by design:

- SMTP delivery
- Background hosted scheduler
- Reports and exports
- Migration baseline/adoption

## Priority 4A Completion Update

Completion date: 2026-06-06

Priority 4A is complete:

- Startup now uses `EnsureCreatedAsync` only; mixed migration fallback behavior was removed.
- `DbSeeder.SeedAsync` seeds data only and no longer initializes schema.
- Relative SQLite paths resolve from the desktop executable directory.
- Fatal initialization or seeding errors stop startup.
- `KicsitLibrary.Tests` is configured with xUnit, `Microsoft.NET.Test.Sdk`, the Visual Studio runner, and EF Core SQLite.
- Nine tests pass against isolated temporary SQLite databases.
- Coverage includes duplicate issue prevention, return availability, non-negative fines, overdue-day calculations, duplicate accession rejection, fresh-database seeding, notification-record persistence without sending, and activity-log writes.
- Clearance blocking coverage remains pending because no clearance service/helper exists.
- EF migrations remain pending until a safe baseline/adoption strategy is designed for databases created with `EnsureCreatedAsync`.

Verification:

```powershell
dotnet build KicsitLibrary.slnx
dotnet test KicsitLibrary.slnx
```

Result: build succeeded with 0 warnings and 0 errors; 9 tests passed.

Audit date: 2026-06-06  
Scope: Read-only continuation audit plus this report. No production code was changed.

## Audit Inputs

Reviewed:

- `PROJECT HANDOFF.md`
- `AGENTS.md`
- `CURRENT STATUS.md`
- `NEXT TASKS.md`
- `TEST COMMANDS.md`
- `KNOWN ISSUES.md`

Requested but not present in the repository:

- `implementation_plan.md`
- `task.md`
- `walkthrough.md`

The solution contains six projects: Core, Data, Services, Desktop, Reports, and Tests.

## 1. Build Result

Command:

```powershell
dotnet build KicsitLibrary.slnx --nologo
```

Result: **Succeeded**

- 0 warnings
- 0 errors
- All six projects restored and compiled.
- The machine used .NET SDK `10.0.300` to build projects targeting .NET 8.
- There is no `global.json` to pin the intended SDK.

## 2. Test Result

Commands:

```powershell
dotnet test KicsitLibrary.slnx --nologo --no-restore
dotnet test KicsitLibrary.Tests/KicsitLibrary.Tests.csproj --no-restore --verbosity diagnostic
```

Result: **No tests ran**

`dotnet test` returns exit code 0, but this must not be treated as a passing test suite. `KicsitLibrary.Tests`:

- Contains only an empty `Class1`.
- Has no `Microsoft.NET.Test.Sdk` package.
- Has no xUnit, NUnit, or MSTest package.
- Does not set `IsTestProject`.
- Contains no test methods.

The diagnostic result explicitly states that the project was skipped because it is not configured as a test project.

## 3. App Startup Risk

Overall startup risk: **High on a fresh installation; medium on an already-created compatible database.**

Main risks:

1. `App.xaml.cs` calls `Database.MigrateAsync()` even though no migrations exist.
2. It falls back to `EnsureCreatedAsync()` only when `MigrateAsync()` throws. A migration call with no application migrations may not throw, leaving no application schema for seeding.
3. `DbSeeder.SeedAsync()` calls `EnsureCreatedAsync()` again. Mixing migrations and `EnsureCreated` is not a supported schema evolution strategy.
4. A database originally created by `EnsureCreated` cannot safely adopt normal migrations without a deliberate baseline procedure.
5. Database initialization errors show a message but startup continues to seeding and then login, allowing a partially initialized application state.
6. No `KicsitLibrary.db` exists in the checked-out tree. The connection string is relative, so the database location depends on the process working directory.
7. This machine has only .NET 10 runtimes installed. The application targets `net8.0-windows`, so the .NET 8 Desktop Runtime is not confirmed.
8. `appsettings.json` is loaded from `Directory.GetCurrentDirectory()`, which is fragile when launched from a shortcut, installer, or a different working directory.

## 4. Database Creation Method

The application currently uses **both** methods:

- `MigrateAsync()` in `KicsitLibrary.Desktop/App.xaml.cs`
- Fallback `EnsureCreatedAsync()` in `KicsitLibrary.Desktop/App.xaml.cs`
- Unconditional `EnsureCreatedAsync()` in `KicsitLibrary.Data/DbSeeder.cs`

Configured default provider: SQLite.

Configured connection:

```text
Data Source=KicsitLibrary.db
```

SQL Server provider code also remains available through configuration.

## 5. Migration Status

Migration status: **Not implemented**

- No `Migrations` directory exists.
- No migration classes or model snapshot exist.
- EF design and tools packages are referenced.
- The context specifies `KicsitLibrary.Data` as the migration assembly.
- Existing databases created with `EnsureCreated` require a baseline/adoption plan before migrations can be enabled.

## 6. Implemented Modules

Implemented with real entities, services, ViewModels, views, and navigation:

- Authentication and role/permission lookup
- Dashboard statistics
- Catalog search and book master management
- Authors and publishers
- Physical book-copy management
- Student management
- Faculty/staff management
- Visit records
- Consumer profile history
- Book issue/check-out
- Book receive/check-in
- Fine calculation, collection, and waiver
- Activity-log writes
- Soft-delete infrastructure and archive records

The named services all exist:

- `CirculationService`
- `CatalogService`
- `ConsumerService`
- `AuthenticationService`
- `DashboardService`
- `ActivityLogService`

## 7. Partially Implemented Modules

- **Reservations:** Entity, DbSet, history query, renewal check, and create method exist. There is no reservation-management screen or complete queue/availability/expiry workflow.
- **Overdue handling:** Eligibility and return flows calculate overdue state from active issues. There is no scanner, scheduler, persistent overdue transition, reminder service, or reminder screen.
- **Notifications:** Entity, enums, DbSet, relationships by convention, dashboard count, and one enable setting exist. No notification service or delivery implementation exists.
- **Dashboard:** Functional, but overdue count reads `BookCopy.AvailabilityStatus == Overdue`; circulation never sets that status, so the metric is not reliable.
- **Activity logs:** Records are written, but the audit-record navigation target has no ViewModel or view.
- **System settings:** Entity, DbSet, and seed data exist. There is no settings service or screen.
- **Clearance:** Student fields exist, but no clearance service/workflow exists. Faculty/staff has no equivalent clearance state.
- **Audit, inventory, and new arrivals:** Domain entities and dashboard counts exist, but operational services and screens are absent.
- **MVVM compliance:** Main content screens are ViewModel-driven, but several window and catalog actions remain in code-behind, contrary to `AGENTS.md`.

## 8. Missing Modules

- Overdue background engine
- Notification service and email dispatcher
- Overdue reminders ViewModel and view
- Reservation management UI and full lifecycle processing
- Reports and analytics implementation
- PDF, Excel, and CSV exports
- Clearance service and UI
- Audit-record browser
- Inventory management UI/workflow
- System settings UI/service
- Backup and restore
- Supabase synchronization
- Deployment/installer configuration
- Real automated tests

`KicsitLibrary.Reports` is currently an empty foundation project.

## 9. Broken or Risky Areas

1. Fresh-database initialization is unsafe because migrations and `EnsureCreated` are mixed.
2. There is no automated regression coverage for completed production workflows.
3. Overdue truth is duplicated and inconsistent:
   - Active loan means `ReceiveRecord == null`.
   - `IssueRecord` has no status.
   - `BookCopy` stays `Issued` and is not changed to `Overdue`.
   - Dashboard counts the unused `BookStatus.Overdue`.
4. Overdue-day calculations truncate `TotalDays`; policy for partial days and due-date boundaries is undefined.
5. `NotificationRecord` has no `IssueRecordId`, recipient-address snapshot, attempt count, next-attempt time, or deduplication key.
6. SMTP host, port, TLS, sender, and credential settings do not exist.
7. A background service must create a DI scope per scan; it must not capture scoped DbContexts.
8. Multiple app instances could scan and send duplicate reminders against the same SQLite database.
9. SQLite write contention must be handled for background notification writes and normal circulation activity.
10. Dashboard exceptions are swallowed, which can display zero values instead of exposing database failures.
11. Five sidebar routes deliberately resolve to `null`: Overdue, Audit, Inventory, Reports, and Settings.
12. Some visible shell buttons have no commands and are placeholder actions.
13. Several `.xaml.cs` files contain event handlers, window creation, or DataContext wiring beyond `InitializeComponent()`.
14. Several ViewModels directly construct windows instead of resolving registered views through DI.
15. Generated mojibake/emoji text is visible in XAML and may render incorrectly.

## 10. Recommended Next Task

The safest next task is a **Priority 4 foundation slice**, not SMTP delivery first:

1. Decide and document the database migration adoption strategy.
2. Configure a real test project and add SQLite integration tests for active, due, overdue, returned, renewed, and duplicate-reminder cases.
3. Define one overdue query/calculation model based on active `IssueRecord` rows.
4. Implement idempotent notification-record generation and manual reminder processing.
5. Add the Overdue Reminders screen and navigation.
6. Add SMTP delivery with retry/failure tracking.
7. Add the hosted periodic scanner last, after manual processing is verified.

Do not mutate `BookCopy.AvailabilityStatus` to `Overdue` unless the project explicitly chooses that denormalized model and guarantees synchronization. Computing overdue from the active issue and due date is safer.

## 11. Exact Files Needed for Priority 4

New production files:

- `KicsitLibrary.Core/Interfaces/INotificationService.cs`
- `KicsitLibrary.Core/Models/OverdueItem.cs`
- `KicsitLibrary.Services/Notifications/NotificationService.cs`
- `KicsitLibrary.Services/Notifications/OverdueNotificationBackgroundService.cs`
- `KicsitLibrary.Desktop/ViewModels/OverdueRemindersViewModel.cs`
- `KicsitLibrary.Desktop/Views/OverdueRemindersView.xaml`
- `KicsitLibrary.Desktop/Views/OverdueRemindersView.xaml.cs` containing only `InitializeComponent()`

Existing production files requiring changes:

- `KicsitLibrary.Core/Entities/NotificationRecord.cs`
- `KicsitLibrary.Core/Entities/IssueRecord.cs`
- `KicsitLibrary.Data/KicsitLibraryDbContext.cs`
- `KicsitLibrary.Data/DbSeeder.cs`
- `KicsitLibrary.Desktop/App.xaml.cs`
- `KicsitLibrary.Desktop/MainWindow.xaml`
- `KicsitLibrary.Desktop/ViewModels/MainViewModel.cs`
- `KicsitLibrary.Services/KicsitLibrary.Services.csproj` only if an external mail package is selected
- `KicsitLibrary.Desktop/appsettings.json` for non-secret scheduler/mail defaults if configuration is not entirely database-backed

Migration files required after the migration strategy is settled:

- `KicsitLibrary.Data/Migrations/<timestamp>_InitialCreate.cs` or an approved baseline migration
- `KicsitLibrary.Data/Migrations/<timestamp>_InitialCreate.Designer.cs`
- `KicsitLibrary.Data/Migrations/KicsitLibraryDbContextModelSnapshot.cs`

Test files required:

- `KicsitLibrary.Tests/KicsitLibrary.Tests.csproj`
- `KicsitLibrary.Tests/Notifications/NotificationServiceTests.cs`
- `KicsitLibrary.Tests/Notifications/OverdueNotificationBackgroundServiceTests.cs`
- Shared SQLite test fixture files as needed under `KicsitLibrary.Tests/Infrastructure/`

## 12. Exact Risks Before Priority 4

1. **Migration baseline risk:** Adding a normal initial migration can conflict with installations already created by `EnsureCreated`.
2. **Duplicate-send risk:** A repeated scan can create or send the same reminder unless uniqueness and idempotency are designed first.
3. **Wrong-recipient risk:** Member type and nullable member IDs must be validated as exactly one valid borrower.
4. **Missing-recipient risk:** Student/faculty email values may be blank or invalid.
5. **Secret-storage risk:** SMTP credentials must not be committed or seeded as plaintext production values.
6. **Concurrency risk:** Hosted scans and UI writes can contend for SQLite locks.
7. **Lifetime risk:** A singleton hosted service cannot retain `KicsitLibraryDbContext` or another scoped service.
8. **Time-zone risk:** Data is stored and compared in UTC while the institution operates in Pakistan; due-date semantics must be explicit.
9. **Calculation risk:** Current integer truncation may undercount the first overdue day.
10. **Status-model risk:** Dashboard and circulation disagree about whether overdue is computed or stored.
11. **Retry risk:** Failed sends need bounded retries and durable failure reasons.
12. **Shutdown risk:** The scanner must honor cancellation and not block WPF startup or exit.
13. **Observability risk:** Current exception swallowing can hide scanner or database failures.
14. **Regression risk:** Priority 4 touches issue, member, notification, settings, dashboard, DI, and startup behavior with no existing tests.

## Conclusion

Priorities 1 through 3 have meaningful implementations and compile successfully, but "100% complete" should be interpreted as feature implementation rather than production verification because no automated tests run. Priority 4 is currently scaffolded only at the entity, enum, setting, dashboard-count, and navigation-command level. Database initialization and test infrastructure should be stabilized before enabling automated notification delivery.
