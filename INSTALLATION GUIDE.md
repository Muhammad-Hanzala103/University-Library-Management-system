# Installation Guide - Ilm o Kutub System

This guide outlines prerequisites, deployment procedures, and configurations to run the **Ilm o Kutub System** in development or production.

---

## 1. Prerequisites
Before installing, ensure target systems meet the following:
* **Operating System**: Windows 10 or Windows 11 (64-bit).
* **Framework Runtime**: .NET 8.0 Desktop Runtime (win-x64) installed.
* **Database Driver**: SQLite Core libraries (automatically packaged with the executable).

---

## 2. Portable Publish Method
Portable publish is the easiest way to run the application for demonstration and review:
1. Extract the published ZIP package (or navigate to the publish directory compiled via build tools).
2. Ensure `appsettings.json` is located in the same directory as the executable.
3. Double-click `KicsitLibrary.Desktop.exe` to launch the application.

*To compile a fresh portable publish package locally, run the deployment smoke test script:*
```powershell
powershell -ExecutionPolicy Bypass ./scripts/deployment_smoke_test.ps1
```
The output binaries will be packaged into: `artifacts/deployment-smoke/publish/`.

---

## 3. ClickOnce Intranet Rollout Method
For institutional production rollout, deployment is automated using ClickOnce:
1. Open the solution in **Visual Studio 2022**.
2. Right-click **KicsitLibrary.Desktop** and select **Publish**.
3. Choose the **ClickOnce** profile.
4. Set the **Publish Location** to a local staging folder and the **Install Location** to the target university file share (e.g. `\\fileserver\Ilm-o-Kutub\`).
5. Choose **Sign Manifests** and select the university code-signing certificate.
6. Under **Updates**, select *Before the application starts*.
7. Publish the application and copy files to the shared folder. Users can run `setup.exe` to install.

---

## 4. Runtime Storage & Data Mappings
The system protects user data by keeping it out of the installation directory (which may be read-only under `C:\Program Files`):
* **Active Database**: Relocates to `%LOCALAPPDATA%\Ilm-o-Kutub System\KicsitLibrary.db` when `UseReleaseDataRoot=True` is configured in `appsettings.json`.
* **Backups Folder**: Defaults to `%USERPROFILE%\Documents\Ilm-o-Kutub Backups\`.
* **Report Exports**: Saved under `%USERPROFILE%\Documents\Ilm-o-Kutub Reports\`.
* **Clearance Certificates**: Exported to `%USERPROFILE%\Documents\Ilm-o-Kutub Certificates\`.

> [!CAUTION]
> Do NOT manually delete `KicsitLibrary.db`, `-wal`, or `-shm` files. Database restores stage files side-by-side; closing the application and launching it again is required to apply the restored database cleanly.
