# Known Issues, Limitations & Incomplete Components

This document outlines modules that are currently implemented or stubbed, listing limitations, missing validations, and runtime constraints.

---

## 1. Placeholder & Stubbed Logic

### Local Database Seeding Fallback
- **Setting**: The SQLite database fallback uses `EnsureCreatedAsync` if EF migrations are missing or fail during execution.
- **Risk**: While fine for development and testing, `EnsureCreatedAsync` does not support schema updates. Migrations must be added and applied before releasing to staging or production.

### Navigation View Stubs
- The following screens are registered in the main sidebar menu in `MainWindow.xaml` and mapped in `MainViewModel.cs` but do not have XAML Views implemented. They will load as blank/loading templates:
  - `"Overdue Reminders"`
  - `"Audit Records"`
  - `"Inventory Management"`
  - `"Reports & Analytics"`
  - `"System Settings"`

---

## 2. Incomplete Integrations & Gaps

### Notification Alert Engine
- Overdue engine scanning is stubbed. There is no automated cron job or background runner that scans for late items and dispatches actual emails to users; only database entities and local status changes exist.

### PDF & Excel Report Printers
- No actual document rendering logic exists inside the `KicsitLibrary.Reports` project. Exporters to PDF (via iText7) and Excel (via ClosedXML) are not implemented.

### Cloud Integration (Supabase Sync)
- Currently, the database provider runs 100% locally on SQLite. There is no background worker that pushes local SQLite transaction records to a remote Supabase Postgres cloud instance.

### System Backups
- Database backup dump scripts, file replication utilities, and SQL recovery mechanisms are pending implementation.

### Unit Test Project Coverage
- The `KicsitLibrary.Tests` assembly contains only a blank template file (`Class1.cs`). There is no unit test coverage for any service layer class (e.g. Catalog, Consumer, or Circulation).
