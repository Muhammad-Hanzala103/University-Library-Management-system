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
Passed: 100
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

Run only Priority 4D scheduler tests:

```powershell
dotnet test KicsitLibrary.Tests/KicsitLibrary.Tests.csproj --filter "FullyQualifiedName~OverdueSchedulerTests"
```

Scheduler tests use temporary SQLite files, fake SMTP transport, and controllable overdue-service doubles. They do not wait for real scheduler intervals.

Run only Priority 5A report tests:

```powershell
dotnet test KicsitLibrary.Tests/KicsitLibrary.Tests.csproj --filter "FullyQualifiedName~ReportFoundationTests"
```

Report tests use temporary SQLite databases and temporary export folders. They physically create CSV, XLSX, and PDF files and do not require Microsoft Excel.

Run only Priority 5B report tests:

```powershell
dotnet test KicsitLibrary.Tests/KicsitLibrary.Tests.csproj --filter "FullyQualifiedName~Priority5BReportTests"
```

Priority 5B tests cover all eleven advanced providers, all sixteen registered definitions, wide PDF output, Excel metadata/headers, and empty/date-formatted CSV output.

Run only Priority 6A clearance tests:

```powershell
dotnet test KicsitLibrary.Tests/KicsitLibrary.Tests.csproj --filter "FullyQualifiedName~ClearanceWorkflowTests"
```

Clearance tests use isolated temporary SQLite databases and temporary certificate folders. They do not access the development database or require a printer.

Run only Priority 6B reservation tests:

```powershell
dotnet test KicsitLibrary.Tests/KicsitLibrary.Tests.csproj --filter "FullyQualifiedName~ReservationWorkflowTests"
```

Reservation tests use isolated temporary SQLite databases and fake email transport. They cover eligibility, queue order, expiry, cancellation, return availability, notification records, deduplication, missing email, fulfillment, activity logs, and queries without accessing the development database or sending email.

## 3. Manual SMTP Verification

1. Back up the development database before changing settings.
2. Populate `SmtpHost`, `SmtpPort`, `SmtpUseSsl`, `SmtpUser`, `SmtpPassword`, `SmtpFromEmail`, and `SmtpFromName` in `SystemSettings`.
3. Set `EmailNotificationEnabled` to `True`.
4. Open Notification Center and select **Validate Email Settings**.
5. Run the overdue check to create pending email records.
6. Use **Send Selected Email** or **Send All Pending Emails**.
7. Confirm the record status, `SentAt`, `LastAttemptAt`, `RetryCount`, and any `FailureReason`.
8. Confirm an activity-log row exists for each attempted delivery.

## 4. Manual Scheduler Verification

1. Back up the development database before changing settings.
2. Confirm `OverdueSchedulerEnabled`, `OverdueSchedulerRunOnStartup`, and `OverdueSchedulerSendPendingEmails` are `False` by default.
3. Open Overdue Reminders and select **Refresh Scheduler Status**.
4. Set `OverdueSchedulerEnabled=True` in `SystemSettings`.
5. Keep `OverdueSchedulerSendPendingEmails=False` for the first run.
6. Select **Run Scheduler Now** and verify notification records are created without email delivery.
7. Run it again and verify same-day records are not duplicated.
8. Confirm last-run, last-success, message, and running-state values update.
9. Configure valid SMTP settings before enabling `OverdueSchedulerSendPendingEmails=True`.
10. Restart the application only after explicitly enabling `OverdueSchedulerRunOnStartup` if a delayed startup run is required.

## 5. Manual Reports Verification

1. Open **Reports & Analytics** from the sidebar.
2. Search reports by name and confirm the visible report count changes.
3. Confirm all sixteen report cards appear under the seven report categories.
4. Select each Priority 5B report and confirm real database rows appear.
5. Apply text, enum, date, number, and boolean filters where available.
6. Verify invalid date/number ranges show a clear validation message.
7. Confirm **Clear Filters** restores the full preview.
8. Export CSV, Excel, and PDF.
9. Confirm the success message includes the generated file path.
10. Open `%USERPROFILE%\Documents\KICSIT Library Reports`.
11. Verify the file name includes the report name and timestamp.
12. Confirm repeated exports create unique files instead of overwriting.
13. Confirm wide PDF reports have page metadata, repeated headers, and page numbers.
14. Confirm Excel reports have metadata, filters, frozen headers, and summaries.
15. Confirm a `Report Exported` activity-log record was written.

## 6. Database Initialization
The current development strategy is `EnsureCreatedAsync` only.

Do not run `dotnet ef migrations add InitialCreate` against the current database workflow. A baseline/adoption plan is required first because existing databases may have been created by `EnsureCreatedAsync`.

Priority 4B adds notification columns through a fixed non-destructive SQLite compatibility initializer. This is not a replacement for the pending migration baseline.

Priority 6A uses the same initializer to add faculty/student clearance columns and indexes without deleting or rebuilding existing tables.

## 7. Manual Clearance Verification

1. Open **Library Clearance** from the sidebar.
2. Switch between Student Clearance and Faculty & Staff Clearance.
3. Search and select a member, then select **Check Clearance**.
4. Confirm active issues, pending fines, and loss/damage blockers appear with exact reasons.
5. Enter approval remarks and approve an eligible member.
6. Confirm status, date, and activity-log entry are persisted.
7. Generate the certificate and open `%USERPROFILE%\Documents\KICSIT Library Certificates`.
8. Add a new due after approval and confirm certificate generation is blocked.
9. Enter a revoke reason and revoke clearance.
10. Open Borrowing History and verify issue and clearance history records.

## 8. Manual Reservation Verification

1. Open **Reservations** from the sidebar.
2. Create reservations for active, uncleared student and faculty/staff members.
3. Confirm duplicate reservations, same-title active issues, and pending fines are blocked.
4. Open a title queue and confirm first-come-first-served positions.
5. Return a normally issued copy and confirm the first queue item becomes `Available`.
6. Confirm no book is issued automatically and no email is sent automatically.
7. Open Notification Center and confirm in-app and email records exist without duplicates.
8. Confirm a missing member email produces a failed email record with a clear reason.
9. Fulfill the first queue item and confirm an issue record is created, the copy becomes `Issued`, and the reservation stores the accession number with status `Issued`.
10. Cancel or expire an active reservation with a reason and confirm an activity-log row is written.

---

## 9. Run the WPF Desktop Application
Launch the WPF UI:
```powershell
dotnet run --project KicsitLibrary.Desktop
```

---

## 10. SQLite Local Database Inspections
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
