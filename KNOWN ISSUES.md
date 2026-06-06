# Known Issues, Limitations & Incomplete Components

This document outlines modules that are currently implemented or stubbed, listing limitations, missing validations, and runtime constraints.

---

## 1. Placeholder & Stubbed Logic

### Database Initialization Strategy
- **Decision**: Priority 4A uses `EnsureCreatedAsync` only. `MigrateAsync` is not called.
- **Reason**: Existing development databases may have been created by `EnsureCreatedAsync`; adding a normal initial migration could conflict with those schemas.
- **Path**: `Data Source=KicsitLibrary.db` resolves to `KicsitLibrary.Desktop/bin/<Configuration>/net8.0-windows/KicsitLibrary.db` when running from build output.
- **Limitation**: `EnsureCreatedAsync` does not evolve an existing schema. A reviewed baseline/adoption plan remains required before staging or production migrations.
- **Data safety**: Startup never deletes, recreates, or automatically relocates an existing database. Databases previously created under a different working directory must be identified and moved only through an explicit backup/restore procedure.
- **Failure behavior**: Database initialization and seeding failures are fatal and stop startup instead of opening the application in a partial state.

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

### Automated Test Coverage
- Nine xUnit tests run against isolated temporary SQLite files.
- Coverage currently protects core circulation transitions, duplicate accession validation, seed insertion, notification-record persistence, activity logging, and overdue/fine calculation foundations.
- There is no clearance service or clearance eligibility helper, so the requested "active issue blocks student clearance" test is pending Priority 6.
- The suite does not yet cover UI behavior, concurrency, reservations, migration adoption, or real notification delivery.
