# Release Test Plan

Purpose: manual and automated verification checklist for deployment readiness. This plan must be executed before packaging or publishing Ilm-o-Kutub System.

## 1. Fresh Install Simulation

- Use a clean Windows machine or VM.
- Install .NET 8 Desktop Runtime if testing framework-dependent output.
- Copy or install the published application.
- Confirm no old `KicsitLibrary.db` exists in the target data location.
- Confirm the app starts without requiring Visual Studio.

## 2. First Run Database Creation

- Launch the application.
- Confirm `KicsitLibrary.db` is created in the expected location.
- Confirm startup applies compatibility initialization without errors.
- Confirm `SystemSettings`, roles, permissions, and default users are seeded.

## 3. Login Test

- Login using a seeded admin account.
- Confirm the main window opens.
- Confirm the title and visible branding are **Ilm-o-Kutub System**.
- Confirm default passwords are changed or documented as a required pre-production action.

## 4. Catalog Test

- Open Book Catalog.
- Create or edit book metadata.
- Add a copy with a unique accession number.
- Confirm duplicate accession numbers are rejected.
- Confirm search and filters work.

## 5. Consumer Test

- Add a student.
- Add a faculty/staff member.
- Open profile/history screens.
- Confirm generated library-card barcode/QR behavior is intact.

## 6. Circulation Test

- Issue an available copy.
- Confirm the same copy cannot be issued twice.
- Receive the copy.
- Confirm availability returns and overdue/fine calculations are correct.

## 7. Reservation Test

- Create a reservation for an eligible member.
- Confirm duplicate reservation and same-title active issue rules.
- Mark a returned copy available for the queue.
- Fulfill, cancel, and expire representative reservations.

## 8. Clearance Test

- Check an eligible student and faculty/staff member.
- Confirm active issues, pending fines, and unresolved loss/damage block clearance.
- Approve with remarks.
- Generate certificate PDF.
- Revoke with reason.

## 9. Reports Export Test

- Open Reports.
- Generate representative reports including Catalog, Issued Books, Student Clearance, Audit, Inventory, SOP Documents, and National Library Rates Documents.
- Export CSV, Excel, and PDF.
- Confirm exports are written under `%USERPROFILE%\Documents\Ilm-o-Kutub Reports`.

## 10. Backup Test

- Open Backup.
- Create a verified backup.
- Confirm `.db` and metadata files are created under `%USERPROFILE%\Documents\Ilm-o-Kutub Backups` by default.
- Confirm SHA-256 and integrity status are shown.

## 11. Restore Test

- Preview a valid backup.
- Stage restore with a reason and exact confirmation.
- Confirm safety backup is created.
- Restart the app.
- Confirm startup applies restore or reports failure clearly.

## 12. Automatic Backup Test

- Confirm automatic backup is disabled by default.
- Enable automatic backup in the UI/settings path available for the release.
- Run backup now.
- Confirm no automatic backup runs while a restore is pending.
- Confirm retention preview protects required files.

## 13. Ownership Lock Test

- Start one application instance.
- Attempt a second instance.
- Confirm single-instance behavior follows settings.
- Run competing backup/restore/scheduler operations where practical.
- Confirm blocked operations show the documented lock message.

## 14. Documents Upload Test

- Upload valid PDF and PNG files.
- Confirm EXE and disallowed extensions are rejected.
- Confirm stored files go under `%USERPROFILE%\Documents\Ilm-o-Kutub System\Documents` by default.
- Confirm open/copy/delete/restore actions log activity.
- Confirm missing physical files show `Missing File`.

## 15. Audit Logs Test

- Perform representative actions in catalog, circulation, backup, restore, and documents.
- Open Activity Logs.
- Confirm actions are visible and filterable.
- Open Audit Records and create/update/delete a record with required reasons.

## 16. Settings Test

- Confirm `appsettings.json` is present beside the executable.
- Confirm database provider is SQLite.
- Confirm SMTP is disabled by default and has no real password.
- Confirm scheduler and automatic backup defaults are disabled.
- Confirm ownership settings are seeded.

## 17. Uninstall Cleanup Expectation

- Uninstall/remove application binaries only.
- Confirm user data is preserved by default:
  - SQLite database
  - Backups
  - Documents
  - Reports
  - Certificates
  - Pending restore metadata
- Data deletion must be explicit, not automatic.

## 18. Upgrade Simulation

- Create real data in the previous build.
- Create a verified backup.
- Replace binaries with the new build.
- Launch the app.
- Confirm database compatibility initialization runs safely.
- Confirm old data remains accessible.
- Confirm rollback procedure is available if startup fails.

## 19. Offline Operation

- Disconnect network.
- Launch and use catalog, circulation, reports, documents, backup, and restore.
- Confirm SMTP delivery fails gracefully or remains disabled.
- Confirm no cloud dependency blocks local work.

## 20. Failure Recovery

- Test missing/corrupted backup file restore rejection.
- Test missing stored document file status.
- Test blocked second-instance or critical-operation lock.
- Test invalid SMTP settings validation.
- Confirm fatal database initialization errors stop startup instead of opening a partial app.
