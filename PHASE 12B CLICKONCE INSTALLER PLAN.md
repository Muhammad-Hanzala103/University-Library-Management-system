# Phase 12B: ClickOnce Installer and Automatic Update Plan

This document outlines the internal university deployment packaging and automatic update strategy for **Ilm-o-Kutub System**.

---

## 1. ClickOnce Purpose

ClickOnce is a deployment technology designed for Windows desktop applications that enables users to install and run the application with minimal user interaction. For the **Ilm-o-Kutub System**, ClickOnce provides:
- **Low-friction installations**: Users can install the system from a shared network folder or an internal university intranet site.
- **Automated application updates**: The application checks for updates on startup, ensuring that librarians and administrative staff are always running the latest authorized version.
- **Safe sandboxed installations**: The application runs under user privileges and does not require local administrator rights to install or update, aligning with typical university IT restriction policies.

---

## 2. Internal University Deployment Folder Approach

For campus-wide distribution, the publish location is mapped to a secure, shared network directory (UNC path) or an internal IIS-hosted web server endpoint.

- **Intranet UNC Path**: `\\fileserver.university.edu\shares\Ilm-o-Kutub\Deploy\`
- **Intranet HTTP URL**: `http://ilmokutub-update.university.edu/deploy/`

The publish output will consist of:
1. The **setup.exe** bootstrapper.
2. The **KicsitLibrary.Desktop.application** deployment manifest.
3. An **Application Files** subdirectory containing version-specific resources (e.g., `KicsitLibrary.Desktop_1_0_0_0`).

---

## 3. Versioning Strategy

ClickOnce versions are independent of the assembly version but will be kept in sync to maintain clarity.
- **Format**: `1.0.0.x` (where `x` represents the ClickOnce revision number).
- **Control**: Incremented automatically on each publish via Visual Studio or manually updated in the publish profile settings.
- **Compatibility Guideline**: Any change to the database schema or configuration format requires a minor or major version bump, prompting coordination with the database administrator before rollout.

---

## 4. Update Strategy

- **Update Polling**: The application checks for updates before starting up. If a new version is available on the deployment path, the user is prompted to install it.
- **Mandatory Updates**: For critical security patches or database schema migrations, updates are marked as **Mandatory** with a minimum required version. This forces update installation before the application runs.
- **Silent Background Updates**: Alternatively, background checks can download updates while the app runs, prompting the user to apply them on the next restart.

---

## 5. Rollback Behavior

If an update fails to launch, or if a critical bug is discovered after update deployment:
- **Application Rollback**: The application stores the previous version on the user's system. Users can roll back to the previous stable state via **Windows Settings > Installed Apps** by choosing the repair/rollback option for Ilm-o-Kutub System.
- **Data Protection**: Because user data (`KicsitLibrary.db`) is kept in `%LOCALAPPDATA%\Ilm-o-Kutub System` and not inside the application package directory, rolling back the application binaries will not lose or corrupt the active library records.

---

## 6. Prerequisite Requirements

Before installing, the ClickOnce bootstrapper verifies the presence of:
- **.NET 8.0 Desktop Runtime** (specifically the `win-x64` version).
- **SQLite Core dependencies** (which are bundled inside the publish directory).

If the .NET 8.0 Desktop Runtime is missing, the bootstrapper offers to redirect the user to Microsoft's official download site or host the installer centrally on the university network.

---

## 7. Signing Certificate Requirement

All ClickOnce manifests must be signed to prevent Windows SmartScreen and security alerts:
- **Development / Internal Testing**: A self-signed test certificate (`KicsitLibrary.Desktop_TemporaryKey.pfx`) can be generated.
- **Production Rollout**: A valid certificate issued by the University's Enterprise Certificate Authority (CA) must be used. Using an unsigned package or a fake/expired certificate will trigger warnings or block installation on university-managed computers.

---

## 8. App Data Storage Warning

> [!WARNING]
> Do NOT store runtime database files (`KicsitLibrary.db`) or document attachments inside the ClickOnce application directory. ClickOnce replaces this directory on every update, which would result in complete data loss.
> All production data must map to `%LOCALAPPDATA%\Ilm-o-Kutub System` by ensuring that `UseReleaseDataRoot=True` is enabled in `appsettings.json` for installer builds.

---

## 9. Backup Before Update Policy

Before any client update is performed:
1. Administrators should perform a manual database backup via the **Backup Management** UI.
2. The automatic background scheduler will execute a backup on first startup of the new version to preserve data integrity.
3. The database relocation service will guard any migrations and verify SQLite integrity (`PRAGMA integrity_check`).

---

## 10. Offline Usage Behavior

ClickOnce is configured to support offline mode. Once installed, users can launch the application from the desktop shortcut or Start Menu without requiring a live network connection to the deployment server. If the server is offline, the update check is safely bypassed after a short timeout.

---

## 11. Visual Studio Publishing Steps (Manual Profile Setup)

Creating a publish profile locally via code is highly fragile due to environment variations. Follow these steps in Visual Studio 2022 to publish the package:

1. Right-click the **KicsitLibrary.Desktop** project and select **Publish**.
2. Select **Folder** or **ClickOnce** as the target, then click **Next**.
3. Set the **Publish location** (e.g., a local staging path like `bin\Release\app.publish\`).
4. Set the **Install location** (the university network share path or URL).
5. Under **Settings**, select **Offline** availability.
6. Under **Manifests**, select your certificate for signing (or click *Create Test Certificate*).
7. Under **Updates**, check *The application should check for updates* and choose *Before the application starts*.
8. Click **Publish** to build the application and package the ClickOnce files.
9. Copy the generated files from the staging path to the university network share.

---

## 12. Manual Checklists

### Manual Install Checklist
- [ ] Ensure target PC is connected to the university intranet.
- [ ] Open the shared folder or installation URL.
- [ ] Double-click `setup.exe` or click the *Install* button on the webpage.
- [ ] Approve the installation warning dialog.
- [ ] Verify the application launches successfully and displays the login screen.

### Manual Update Checklist
- [ ] Publish the new version (`1.0.0.x`) to the installation share.
- [ ] Launch the application on a client machine.
- [ ] Verify that the update prompt appears.
- [ ] Allow the update to complete.
- [ ] Check **Settings** inside the application to verify the version matches the updated value.

### Manual Rollback Checklist
- [ ] On the client PC, go to **Windows Settings > Installed Apps**.
- [ ] Find **Ilm-o-Kutub System** and select **Modify/Uninstall**.
- [ ] Choose the option to **Restore the application to its previous state**.
- [ ] Confirm and launch the application to verify it runs under the previous version.
