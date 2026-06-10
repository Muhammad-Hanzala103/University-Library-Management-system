# Release Notes - Ilm o Kutub System

## Version 1.0.0 (Internal Demo Build)
**Release Date**: June 10, 2026  
**Build Target**: net8.0-windows  
**Status**: Internal University Demo Ready

---

## 1. Feature Summary
This release marks the baseline completion of the **Ilm o Kutub System** desktop university library system.
* **Splash Screen Startup Polish**: Centered launch screen showing loading messages and assembly version.
* **Catalog & Circulation**: Author, publisher, subcategory hierarchy validations, and checkout/return modes (Pay Now, Pay Later, Waive).
* **Database & File Security**: Singleton Mutex instance lock, database-level operations lease locks, and verified staged database restore staging.
* **Clearanace Certifications**: Automated library clearance reviews blocking on active borrows or fines, producing signed PDF clearance certificates.
* **Reports and Analytics**: 16 distinct administrative reports exporting to CSV, ClosedXML Excel, and custom PDF.
* **Secure Document Upload**: Administrative document vault with allowed extension and file-signature verification.

---

## 2. Build & Test Status
* **Compilation**: Succeeded with **0 warnings** and **0 errors** on clean build.
* **Automated Tests**: **284 integration tests** executed and passed successfully.
* **Smoke Testing**: Validated executable, appsettings.json, required core assemblies, and database isolation.

---

## 3. Splash Screen Integration
During startup, the application displays a borderless `SplashWindow` to manage user expectations during heavy operations. It handles initialization, locks checking, database staging, and migrations cleanly, shutting down and reporting error dialogs if fatal faults occur.

---

## 4. Upgrade & Update Notes
* **ClickOnce Rollout**: Clients pull updates automatically before startup from the university intranet server.
* **Data Relocation**: Database stays in `%LOCALAPPDATA%\Ilm-o-Kutub System` to prevent updates from overwriting active files.

---

## 5. Backup Before Update Instruction
> [!IMPORTANT]
> Always perform a manual verified database backup via the **Backup Management** UI before modifying the publish folder or installing a new client version. Check that the `PRAGMA integrity_check` passes and preserve the SHA-256 hash.

---

## 6. Known Limitations
* **Local Database**: Supabase cloud sync is planned but deferred; the system operates purely offline on SQLite.
* **Certificates**: Manifests use a development test certificate. Active university-wide rollouts must be signed by the university Enterprise CA.
* **SMTP Secrets**: Settings are saved in SQLite settings; dynamic encryption of configuration settings is deferred.
