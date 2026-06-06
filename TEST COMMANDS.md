# Build, Test, and Database Inspection Commands

Use the following commands from the repository root (`c:\Projects\University Library Management system\`) to build, run, and maintain the system.

---

## 1. Project Compilation
Build the entire solution containing all projects:
```powershell
dotnet build KicsitLibrary.slnx
```

Clean the solution binaries:
```powershell
dotnet clean KicsitLibrary.slnx
```

---

## 2. Test Execution
Run the unit test project (ensure tests are discovered):
```powershell
dotnet test KicsitLibrary.slnx
```

Run test execution with logging:
```powershell
dotnet test KicsitLibrary.slnx --logger "console;verbosity=detailed"
```

The test project uses a unique temporary SQLite file per test under `%TEMP%\KicsitLibrary.Tests`. It never opens `KicsitLibrary.db`.

Current expected result:

```text
Passed: 29
Failed: 0
Skipped: 0
```

Run only Priority 4B tests:

```powershell
dotnet test KicsitLibrary.Tests/KicsitLibrary.Tests.csproj --filter "FullyQualifiedName~OverdueNotificationTests|FullyQualifiedName~DashboardOverdueTests"
```

Run only Priority 4C email-delivery tests:

```powershell
dotnet test KicsitLibrary.Tests/KicsitLibrary.Tests.csproj --filter "FullyQualifiedName~EmailDeliveryTests"
```

Priority 4C tests use `FakeEmailTransport`; they never open a network connection or use the development database.

## 3. Manual SMTP Verification

1. Back up the development database before changing settings.
2. Populate `SmtpHost`, `SmtpPort`, `SmtpUseSsl`, `SmtpUser`, `SmtpPassword`, `SmtpFromEmail`, and `SmtpFromName` in `SystemSettings`.
3. Set `EmailNotificationEnabled` to `True`.
4. Open Notification Center and select **Validate Email Settings**.
5. Run the overdue check to create pending email records.
6. Use **Send Selected Email** or **Send All Pending Emails**.
7. Confirm the record status, `SentAt`, `LastAttemptAt`, `RetryCount`, and any `FailureReason`.
8. Confirm an activity-log row exists for each attempted delivery.

## 4. Database Initialization
The current development strategy is `EnsureCreatedAsync` only.

Do not run `dotnet ef migrations add InitialCreate` against the current database workflow. A baseline/adoption plan is required first because existing databases may have been created by `EnsureCreatedAsync`.

Priority 4B adds notification columns through a fixed non-destructive SQLite compatibility initializer. This is not a replacement for the pending migration baseline.

---

## 5. Run the WPF Desktop Application
Launch the WPF UI:
```powershell
dotnet run --project KicsitLibrary.Desktop
```

---

## 6. SQLite Local Database Inspections
The default SQLite database is named `KicsitLibrary.db`. Relative paths are resolved from `AppContext.BaseDirectory`, normally `KicsitLibrary.Desktop/bin/Debug/net8.0-windows/`.
- Connect to database using SQLite CLI:
  ```bash
  sqlite3 KicsitLibrary.db
  ```
- Query Seeded Accounts:
  ```sql
  SELECT Username, Email, IsActive FROM Users;
  ```
- Inspect Active Library Settings:
  ```sql
  SELECT SettingKey, SettingValue FROM SystemSettings;
  ```
- Check Unpaid Fine Totals:
  ```sql
  SELECT FineRecordNumber, RemainingAmount, PaymentStatus FROM Fines WHERE PaymentStatus = 'Unpaid';
  ```
