# Build, Test, and Database Inspection Commands

Use the following commands from the repository root (`c:\Projects\University Library Management system\`) to build, run, and maintain the system.

---

## 1. Project Compilation
Build the entire solution containing all projects:
```bash
dotnet build
```

Clean the solution binaries:
```bash
dotnet clean
```

---

## 2. Test Execution
Run the unit test project (ensure tests are discovered):
```bash
dotnet test
```

Run test execution with logging:
```bash
dotnet test --logger "console;verbosity=detailed"
```

---

## 3. Database Initializer & Migration Scripts
Because the database is SQLite, you can manage it using Entity Framework core commands:
- Install Entity Framework CLI Tools:
  ```bash
  dotnet tool install --global dotnet-ef
  ```
- Generate an Initial Migration:
  ```bash
  dotnet ef migrations add InitialCreate --project KicsitLibrary.Data --startup-project KicsitLibrary.Desktop
  ```
- Apply Migration updates to Database:
  ```bash
  dotnet ef database update --project KicsitLibrary.Data --startup-project KicsitLibrary.Desktop
  ```

---

## 4. Run the WPF Desktop Application
Launch the WPF UI:
```bash
dotnet run --project KicsitLibrary.Desktop
```

---

## 5. SQLite Local Database Inspections
The default SQLite database is named `KicsitLibrary.db` and is created in the executing output directory (usually `/KicsitLibrary.Desktop/bin/Debug/net8.0-windows/`).
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
