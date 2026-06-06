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

---

## 3. Database Initialization
The current development strategy is `EnsureCreatedAsync` only.

Do not run `dotnet ef migrations add InitialCreate` against the current database workflow. A baseline/adoption plan is required first because existing databases may have been created by `EnsureCreatedAsync`.

---

## 4. Run the WPF Desktop Application
Launch the WPF UI:
```powershell
dotnet run --project KicsitLibrary.Desktop
```

---

## 5. SQLite Local Database Inspections
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
