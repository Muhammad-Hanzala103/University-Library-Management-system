# Demo Checklist - Ilm o Kutub System

Follow these steps to demonstrate the primary features of the **Ilm o Kutub System** during the university evaluation or lab presentation.

---

## 1. Application Launch & Startup Polish
* [ ] **Splash Screen**: Launch the application. Verify that a centered borderless splash screen appears immediately.
* [ ] **Dynamic Statuses**: Check that the status label displays:
  * *Starting application...*
  * *Loading configuration...*
  * *Checking database locks...*
  * *Preparing database...*
  * *Initializing services...*
  * *Opening secure login...*
* [ ] **Version Label**: Confirm the dynamic version number matches the assembly version in the top-right corner.
* [ ] **Clean Transition**: Confirm that the splash screen closes completely before the Login window appears.

---

## 2. Authentication & Navigation
* [ ] **Secure Login**: Sign in using the default administrator credentials:
  * **Username**: `admin`
  * **Password**: `Admin123!` (or the seeded credentials documented in the source code).
* [ ] ** conciseness check**: Verify that the sidebar navigation labels are clean and concise (Dashboard, Book Catalog, Issue Material, Receive Material, etc.).
* [ ] **Helpful Hints**: Toggle the **Show Helpful Hints** setting in the top bar. Hover over buttons and verify that tooltips toggle on and off.

---

## 3. Cataloging & Member Management
* [ ] **Manage Author & Publisher**: Open **Book Catalog**. Navigate to author/publisher popups, add a new author, and verify that duplicate names are blocked.
* [ ] **Add Book & Copy**: Create a new Book Master title. Add a physical copy, verify that duplicate accession numbers are rejected, and set specific rack/shelf locations.
* [ ] **Member Registrations**: Go to the **Students** list. Add a student, typing invalid phone or email formats to verify validation checks. Verify duplicate email blocking.

---

## 4. Circulation & Fine Management
* [ ] **Issue Material**: Go to **Issue Material**. Check out a copy to a student. Confirm that the book status changes to `Issued`.
* [ ] **Receive Material (No Fine)**: Go to **Receive Material**. Return a book that is not overdue. Confirm the copy status becomes `Available`.
* [ ] **Receive Material (With Overdue Fines)**: Return a book past its due date. Verify the fine is calculated automatically.
* [ ] **Return Modes**: Demonstrate processing a return under:
  * **Pay Now**: Records the payment and clears the fine balance immediately.
  * **Pay Later**: Returns the book copy to service but preserves the fine balance.
  * **Waive**: Waives the fine, recording remarks in the audit logs.

---

## 5. Overdue Scanning & Reminders
* [ ] **Overdue Reminders**: Navigate to **Overdue Reminders**.
* [ ] **Filters**: Test filtering by Department, Date Range, Active/Returned, and Resolved/Pending status.
* [ ] **Background Scheduler**: Verify that the status of the scheduler background worker is displayed.

---

## 6. Administrative Reports
* [ ] **Reports Dashboard**: Open **Reports**. Confirm that 16 distinct reports are organized under 7 categories.
* [ ] **Preview & Export**: Run a report (e.g. *Issued Books Report*), apply search filters, and export to **CSV**, **Excel**, and **PDF**.
* [ ] **Verification**: Open the output files under `%USERPROFILE%\Documents\Ilm-o-Kutub Reports` to verify wide-table landscape columns (PDF) and formatted currency fields (Excel).

---

## 7. Local Backups & Restores
* [ ] **Create Backup**: Open **Backup**. Enter a backup reason and click **Create Backup**. Verify that a verified `.db` and `.metadata.json` are created under `Documents\Ilm-o-Kutub Backups`.
* [ ] **Integrity Checks**: Confirm that the backup row shows a `Passed` status and SHA-256 hash.
* [ ] **Stage Restore**: Select a backup, click **Restore Selected**, and type `RESTORE` to stage it. Close the application, relaunch, and confirm that the restore was applied at startup.

---

## 8. Settings Management
* [ ] **Configure Policies**: Open **Settings**. Modify fine rates per day, email SMTP settings, and folder mappings. Verify changes write directly to database settings.

---

## 9. Smoke Script Validation
* [ ] **Smoke Test**: Run the automated dry run command to verify package readiness:
  ```powershell
  powershell -ExecutionPolicy Bypass ./scripts/deployment_smoke_test.ps1
  ```
  Verify that all checks return `[PASS]`.
