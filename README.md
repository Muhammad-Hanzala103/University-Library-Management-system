# Ilm o Kutub System
## Advanced University Library Management System

* **Course**: Advanced Visual Programming Lab
* **Submitted to**: Sir Uzair
* **Institution**: Dr A Q Khan Institute of Computer Sciences and Information Technology (KICSIT)
* **Group Members**:
  * Muhammad Hanzala (Roll No: 232201020)
  * Muhammad Umar (Roll No: 232201008)
  * Muhammad Umair (Roll No: 232201007)

---

## 1. Project Overview
**Ilm o Kutub System** is a professional-grade, university-tailored Desktop Library Operations and Administration System. Built using .NET 8 WPF under strict MVVM design patterns, it is designed to manage high-volume campus cataloging, student/faculty circulation, clearance certifications, local secure backup/restore operations, multi-instance lock protections, and extensive administrative reporting.

## 2. Problem Statement
Many university libraries rely on legacy, uncoordinated systems or manual record-keeping. These processes lead to:
* High rates of unreturned materials and uncollected fines.
* Data corruption due to concurrent program access or improper shutdowns.
* Severe operational bottlenecks during semester-end student/faculty library clearance.
* Lack of offline-resilient local database backup and validation procedures.
* Fragmented reporting for catalogs, active circulation, and student financial obligations.

## 3. Existing University Library Limitations
* **Weak Circulation Validation**: Systems allow books to be issued to students with massive outstanding fines or duplicate holds.
* **Vulnerable Database Files**: SQLite database writes lack ownership checks, leading to corruption when multiple instances run simultaneously.
* **Inefficient Clearance Auditing**: Academic offices manually coordinate library clearance, delaying degree processing.
* **Unreliable Backups**: Basic file copies do not verify SQLite transaction logs (`-wal` or `-shm` files) or run integrity checks before archiving.

## 4. Proposed Solution
**Ilm o Kutub System** provides a unified WPF desktop application backed by a thread-safe, local SQLite database provider. It integrates rigid check-out limits, automated fine calculation, staged database restores with startup verification, strict single-instance mutex locks, dynamic clearance checklists, and dynamic report exports.

## 5. Key Features
* **Splash Screen Startup Polish**: Professional borderless launch screen displaying dynamic assembly versioning and service initialization status.
* **Unified Cataloging**: Comprehensive master titles, accession number copy tracking, rack/shelf location logging, and category hierarchies.
* **Robust Circulation**: Check-out checking, fine waivers, Pay Now/Later, and soft-delete archive logs.
* **Deterministic Overdue Scanning**: Periodic background scanner with database lock-retry handling.
* **Academic Clearance Workflow**: Multi-point clearance check, remarks tracking, and secure PDF certificate exports.
* **Local SQLite Backup & Restore**: Safe online SQLite API backups, SHA-256 integrity verification, ZIP compression, and staged startup replacement with automatic recovery/rollback.
* **Cross-Process Ownership Lock**: Mutex-guarded database and folder operations preventing data races across multiple app instances.
* **Managed Document Upload**: Signature-verified local document vaults protecting official university policies.

## 6. Technology Stack
* **Framework**: .NET 8.0 WPF (Windows Desktop Application)
* **Language**: C# 12
* **MVVM Engine**: `CommunityToolkit.Mvvm` (using source generators for properties/commands)
* **ORM**: Entity Framework Core 8.0 (SQLite Provider)
* **Office Integration**: `ClosedXML` (for Excel generation)
* **SMTP Transport**: `MailKit` (for email notifications)

## 7. Architecture
The project is structured as a clean 6-project C# solution:
* `KicsitLibrary.Core`: Domain models, enums, interfaces, and branding tokens.
* `KicsitLibrary.Data`: EF Core DbContext, repositories, seeder, and compatibility initializers.
* `KicsitLibrary.Services`: Business services, scheduler, document handling, and backup/restore logic.
* `KicsitLibrary.Reports`: Provider implementations, CSV, Excel, and PDF exporters.
* `KicsitLibrary.Desktop`: WPF UI views, ViewModels, and app settings.
* `KicsitLibrary.Tests`: Isolated xUnit integration tests.

## 8. Modules
* **Dashboard**: Key statistics showing catalog count, active issues, damaged books, and registration totals.
* **Book Catalog**: CRUD views for authors, publishers, categories, and physical copies.
* **Issue & Receive Material**: Circulation workflow verifying eligibility and executing checkout/return.
* **Library Clearance**: Audits members for active obligations and generates PDF certificates.
* **Backup & Restore**: Controls to schedule automatic backups, stage restores, and clear lock files.
* **Documents**: Managed repository for official library rules, rates, and policies.
* **Settings**: Administration controls for fine policies, SMTP details, and folder mappings.

## 9. Database and Runtime Storage
The database remains a local SQLite file (`KicsitLibrary.db`).
* **Development Mode**: Resolves relative to the application base directory (`UseReleaseDataRoot=False`).
* **Release Mode**: Relocates to the user's secure `%LOCALAPPDATA%\Ilm-o-Kutub System` directory (`UseReleaseDataRoot=True`), protecting it from write restrictions inside `Program Files`.

## 10. Security and Authorization
* **Password Hashing**: PBKDF2 hashing via `IPasswordHasher` prevents plain-text exposure in the database.
* **Role-Based Access Control**: Standardized permissions for Super Admin, Admin, Librarian, Assistant, Auditor, and Viewer roles.
* **Credential Protection**: Dynamic SMTP configurations are backing database rows; no credentials or passwords are committed in source control.
* **Default Account Policy**: Seeded demo accounts are created during first-run database initialization. Passwords must be changed before any production or campus-wide deployment.
* **SMTP Masking**: Sensitive settings (SMTP password, credentials) are masked in all activity logs, settings exports, and UI displays.

## 11. Backup and Restore
* **Online Backups**: Uses the SQLite native backup API, capturing active transaction snapshots safely.
* **Verification**: Computes SHA-256 hashes and runs `PRAGMA integrity_check`.
* **Staged Restore**: A database restore stages the candidate file, writes sidecar metadata, and prompts the user to restart, applying the changes before EF Core boots.

## 12. Reports and Analytics
Generates 16 distinct administrative reports:
* Output formats include raw CSV, styled Excel (via ClosedXML), and lightweight PDF files.
* Files default to timestamped, non-overwriting locations under the user's `Documents` folder.

## 13. Splash Screen and UI Polish
* Centered borderless launch screen sized 720x420.
* Styled using a dark palette, accenting lines, and horizontal progress bar.
* Displays explicit status updates during runtime data root resolved checks, restore processing, and database seeding.

## 14. Testing Summary
* **Total Automated Tests**: 305 integration tests.
* **Execution**: Run in isolated temporary environments using `%TEMP%` directories.
* **Verification Status**: All 305 tests pass successfully with 0 errors and 0 warnings.

## 15. Build and Run Instructions
To restore, compile, and run the project locally:
```powershell
# Restore dependencies
dotnet restore KicsitLibrary.slnx

# Compile the solution
dotnet build KicsitLibrary.slnx

# Execute unit tests
dotnet test KicsitLibrary.slnx

# Run the WPF Application
dotnet run --project KicsitLibrary.Desktop
```

## 16. Deployment and Packaging
* **Immediate University Demo**: Use framework-dependent **Portable Publish** by copying compiled binaries from the build directory.
* **Campus Rollout**: Recommended signed **ClickOnce** installer using Visual Studio publish profiles. Automated updates are polled before launch from the intranet UNC path or web server.

## 17. Screenshots Placeholder Section
Place local screenshots in this directory with the following naming:
* `docs/screenshots/splash_screen.png` - Centered borderless loading window
* `docs/screenshots/login_screen.png` - Secure credentials window
* `docs/screenshots/dashboard.png` - Live statistics overview
* `docs/screenshots/catalog.png` - Author/publisher and master copy grids
* `docs/screenshots/clearance.png` - Clearance approval screen and PDF certificate

## 18. Demo Checklist
* [ ] **Splash Window**: Confirm loading statuses update smoothly before login.
* [ ] **Login Screen**: Sign in using demo credentials (provided privately — see `DbSeeder.cs`).
* [ ] **Dashboard**: Verify totals match active DB rows.
* [ ] **Circulation Test**: Issue a catalog copy, verify overdue reminder tracking.
* [ ] **Clearance Check**: Verify clearance check blocks members with unresolved checkouts or outstanding fines.
* [ ] **Backup Creation**: Create a manual backup and verify the integrity checks pass.

## 19. Known Limitations
* **Local-Only Database**: SQLite is locally configured; cloud sync (Supabase Sync) remains planned but deferred.
* **Unsigned Profile**: Production ClickOnce packages require a certificate issued by the University CA (self-signed test certificates are for development only).

## 20. Future Enhancements
* **Cloud Sync Integration**: Real-time replication of SQLite transaction history to remote Postgres servers.
* **Credentials Encryption**: Implementing Windows Data Protection API (DPAPI) to encrypt SMTP settings in the database.

## 21. Team Members
* Muhammad Hanzala (232201020)
* Muhammad Umar (232201008)
* Muhammad Umair (232201007)

## 22. License or Academic Use Note
This project has been developed as part of academic coursework for the Advanced Visual Programming Lab at KICSIT. All rights reserved.
