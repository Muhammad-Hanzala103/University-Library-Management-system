using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using KicsitLibrary.Core;
using KicsitLibrary.Core.Entities;
using KicsitLibrary.Core.Interfaces;

namespace KicsitLibrary.Data
{
    public class DbSeeder
    {
        public static async Task SeedAsync(KicsitLibraryDbContext context, IPasswordHasher passwordHasher)
        {
            // 1. Seed Roles
            var roles = new List<Role>
            {
                new() { Name = "Super Admin", Description = "Full system control bypasses all permission checks." },
                new() { Name = "Admin", Description = "System administration, user management, and logs access." },
                new() { Name = "Librarian", Description = "Manages books, library material, issues, returns, and inventory." },
                new() { Name = "Assistant Librarian", Description = "Routine operations: issuing, receiving, searching, and reservations." },
                new() { Name = "Student", Description = "Read-only access to own account, search catalog, view overdue details." },
                new() { Name = "Faculty", Description = "Read-only access to own issued history and search catalog." },
                new() { Name = "Visiting Faculty", Description = "Read-only access to own issued history and search catalog." },
                new() { Name = "Staff", Description = "Read-only access to own issued history and search catalog." },
                new() { Name = "Auditor", Description = "Access to audits, inventory, and visit logs." },
                new() { Name = "Read Only Viewer", Description = "View selected reports and catalog only." }
            };

            foreach (var r in roles)
            {
                if (!await context.Roles.AnyAsync(role => role.Name == r.Name))
                {
                    await context.Roles.AddAsync(r);
                }
            }
            await context.SaveChangesAsync();

            // Resolve role entities with database ids
            var superAdminRole = await context.Roles.FirstAsync(r => r.Name == "Super Admin");
            var adminRole = await context.Roles.FirstAsync(r => r.Name == "Admin");
            var librarianRole = await context.Roles.FirstAsync(r => r.Name == "Librarian");
            var assistantLibrarianRole = await context.Roles.FirstAsync(r => r.Name == "Assistant Librarian");
            var auditorRole = await context.Roles.FirstAsync(r => r.Name == "Auditor");
            var viewerRole = await context.Roles.FirstAsync(r => r.Name == "Read Only Viewer");

            // 2. Seed Permissions
            var permissions = new List<Permission>
            {
                new() { Code = "MANAGE_USERS", Name = "Manage Users", Description = "Add, edit, delete user accounts." },
                new() { Code = "MANAGE_ROLES", Name = "Manage Roles & Permissions", Description = "Change role settings." },
                new() { Code = "MANAGE_BOOKS", Name = "Manage Catalog Books", Description = "Insert and delete catalog books." },
                new() { Code = "ISSUE_BOOK", Name = "Issue Library Material", Description = "Issue books to members." },
                new() { Code = "RECEIVE_BOOK", Name = "Receive Library Material", Description = "Receive returned books." },
                new() { Code = "MANAGE_FINES", Name = "Manage Fines", Description = "Collect or waive fines." },
                new() { Code = "MANAGE_RESERVATIONS", Name = "Manage Reservations", Description = "Handle hold requests." },
                new() { Code = "VIEW_REPORTS", Name = "View Analytics & Reports", Description = "Access reports module." },
                new() { Code = "MANAGE_VISITS", Name = "Manage Visit Logs", Description = "Edit accreditation bodies visit records." },
                new() { Code = "MANAGE_AUDITS", Name = "Manage Audit Logs", Description = "Add and update audit files." },
                new() { Code = "VIEW_AUDITS", Name = "View Audits", Description = "Inspect audit records." },
                new() { Code = "MANAGE_INVENTORY", Name = "Manage Inventory", Description = "Track furniture and hardware." },
                new() { Code = "VIEW_INVENTORY", Name = "View Inventory", Description = "View furniture lists." },
                new() { Code = "MANAGE_SYSTEM", Name = "Manage System Settings", Description = "Change fine rates and metadata." },
                new() { Code = "VIEW_BACKUPS", Name = "View Backups", Description = "View local backup history." },
                new() { Code = "MANAGE_BACKUPS", Name = "Manage Backups", Description = "Create and verify local backups." },
                new() { Code = "MANAGE_AUTOMATIC_BACKUPS", Name = "Manage Automatic Backups", Description = "Configure automatic backups and apply retention." },
                new() { Code = "VIEW_RESTORES", Name = "View Restores", Description = "View local restore history." },
                new() { Code = "MANAGE_RESTORES", Name = "Manage Restores", Description = "Stage verified local database restores." },
                new() { Code = "VIEW_OWNERSHIP_STATUS", Name = "View Ownership Status", Description = "View cross process database ownership status." },
                new() { Code = "MANAGE_OWNERSHIP_STATUS", Name = "Manage Ownership Status", Description = "Cleanup stale ownership lock files." },
                new() { Code = "VIEW_DOCUMENTS", Name = "View Documents", Description = "View document metadata." },
                new() { Code = "OPEN_DOCUMENTS", Name = "Open Documents", Description = "Open or copy stored administrative documents." },
                new() { Code = "MANAGE_DOCUMENTS", Name = "Manage Documents", Description = "Upload, delete, and restore administrative documents." }
            };

            foreach (var p in permissions)
            {
                if (!await context.Permissions.AnyAsync(permission => permission.Code == p.Code))
                {
                    await context.Permissions.AddAsync(p);
                }
            }
            await context.SaveChangesAsync();

            // 3. Seed RolePermissions
            var rolePermissionMappings = new List<(Role role, string[] permissionCodes)>
            {
                (adminRole, new[] { "MANAGE_USERS", "MANAGE_ROLES", "VIEW_REPORTS", "MANAGE_SYSTEM", "VIEW_BACKUPS", "MANAGE_BACKUPS", "MANAGE_AUTOMATIC_BACKUPS", "VIEW_RESTORES", "MANAGE_RESTORES", "VIEW_OWNERSHIP_STATUS", "MANAGE_OWNERSHIP_STATUS", "VIEW_DOCUMENTS", "OPEN_DOCUMENTS", "MANAGE_DOCUMENTS" }),
                (librarianRole, new[] { "MANAGE_BOOKS", "ISSUE_BOOK", "RECEIVE_BOOK", "MANAGE_FINES", "MANAGE_RESERVATIONS", "VIEW_REPORTS", "MANAGE_VISITS", "MANAGE_AUDITS", "VIEW_AUDITS", "MANAGE_INVENTORY", "VIEW_INVENTORY", "VIEW_BACKUPS", "VIEW_RESTORES", "VIEW_OWNERSHIP_STATUS", "VIEW_DOCUMENTS", "OPEN_DOCUMENTS", "MANAGE_DOCUMENTS" }),
                (assistantLibrarianRole, new[] { "ISSUE_BOOK", "RECEIVE_BOOK", "MANAGE_RESERVATIONS", "VIEW_REPORTS" }),
                (auditorRole, new[] { "VIEW_REPORTS", "VIEW_AUDITS", "VIEW_INVENTORY", "VIEW_BACKUPS", "VIEW_RESTORES", "VIEW_OWNERSHIP_STATUS", "VIEW_DOCUMENTS", "OPEN_DOCUMENTS" }),
                (viewerRole, new[] { "VIEW_REPORTS", "VIEW_DOCUMENTS" })
            };

            foreach (var mapping in rolePermissionMappings)
            {
                foreach (var code in mapping.permissionCodes)
                {
                    var permission = await context.Permissions.FirstAsync(p => p.Code == code);
                    var exists = await context.RolePermissions.AnyAsync(rp => rp.RoleId == mapping.role.Id && rp.PermissionId == permission.Id);
                    if (!exists)
                    {
                        await context.RolePermissions.AddAsync(new RolePermission
                        {
                            RoleId = mapping.role.Id,
                            PermissionId = permission.Id
                        });
                    }
                }
            }
            await context.SaveChangesAsync();

            // 4. Seed Default Users
            var usersToSeed = new List<(string username, string password, string fullName, string email, Role role)>
            {
                ("superadmin", "SuperAdmin123!", "Super Administrator", "superadmin@kicsit.edu.pk", superAdminRole),
                ("admin", "Admin123!", "System Administrator", "admin@kicsit.edu.pk", adminRole),
                ("librarian", "Librarian123!", "Head Librarian", "librarian@kicsit.edu.pk", librarianRole),
                ("assistant", "Assistant123!", "Assistant Librarian", "assistant@kicsit.edu.pk", assistantLibrarianRole),
                ("auditor", "Auditor123!", "External Auditor", "auditor@hec.gov.pk", auditorRole),
                ("viewer", "Viewer123!", "Read Only Observer", "viewer@kicsit.edu.pk", viewerRole)
            };

            foreach (var u in usersToSeed)
            {
                if (!await context.Users.AnyAsync(user => user.Username == u.username))
                {
                    var newUser = new User
                    {
                        Username = u.username,
                        PasswordHash = passwordHasher.HashPassword(u.password),
                        FullName = u.fullName,
                        Email = u.email,
                        IsActive = true
                    };
                    await context.Users.AddAsync(newUser);
                    await context.SaveChangesAsync(); // Save user to generate Id

                    // Assign Role
                    await context.UserRoles.AddAsync(new UserRole
                    {
                        UserId = newUser.Id,
                        RoleId = u.role.Id
                    });
                }
            }
            await context.SaveChangesAsync();

            // 5. Seed Book Categories (14 Categories)
            var defaultCategories = new List<string>
            {
                "Computer Science", "Computer Engineering", "General", "Reference",
                "Islamic Studies", "Pakistan Studies", "History", "Urdu Literature",
                "English Literature", "Journals", "Magazines", "Reports", "Thesis", "Project Reports"
            };

            foreach (var catName in defaultCategories)
            {
                if (!await context.Categories.AnyAsync(c => c.Name == catName))
                {
                    await context.Categories.AddAsync(new Category { Name = catName, Description = $"{catName} library section" });
                }
            }

            // 6. Seed Department Categories
            var defaultDeptCategories = new List<string> { "CS", "CE", "SE", "AI", "DS", "General" };
            foreach (var deptName in defaultDeptCategories)
            {
                if (!await context.DepartmentCategories.AnyAsync(dc => dc.Name == deptName))
                {
                    await context.DepartmentCategories.AddAsync(new DepartmentCategory { Name = deptName, Description = $"{deptName} academic department" });
                }
            }

            // 7. Seed Literature Categories
            var defaultLitCategories = new List<string> { "Urdu", "English", "History", "Islam", "Pakistan Studies", "General" };
            foreach (var litName in defaultLitCategories)
            {
                if (!await context.LiteratureCategories.AnyAsync(lc => lc.Name == litName))
                {
                    await context.LiteratureCategories.AddAsync(new LiteratureCategory { Name = litName, Description = $"{litName} literature category" });
                }
            }
            await context.SaveChangesAsync();

            // 8. Seed Default System Settings
            var defaultSettings = new Dictionary<string, (string Value, string Description, string Group)>
            {
                { "FinePerDay", ("10", "Daily overdue fine amount in PKR", "Billing") },
                { "StudentIssueLimit", ("3", "Maximum books a student can borrow", "Borrowing") },
                { "FacultyIssueLimit", ("10", "Maximum books a faculty can borrow", "Borrowing") },
                { "StaffIssueLimit", ("5", "Maximum books a staff member can borrow", "Borrowing") },
                { "DefaultIssueDays", ("14", "Standard issue duration in days", "Borrowing") },
                { "ReservationExpiryDays", ("3", "Days a reservation remains available before expiring", "Reservation") },
                { "NotificationCooldownHours", ("24", "Minimum hours between reminders for the same issue and channel", "Notifications") },
                { "EmailNotificationEnabled", ("False", "Enable or disable email alerts", "Notifications") },
                { "WhatsAppNotificationEnabled", ("False", "Placeholder flag for future WhatsApp gateway integration", "Notifications") },
                { "ReminderBeforeDueDays", ("2", "Days before due date for a future reminder workflow", "Notifications") },
                { "MaxNotificationRetryCount", ("3", "Maximum manual retry attempts for a notification record", "Notifications") },
                { "SmtpHost", ("", "SMTP server host name", "Notifications") },
                { "SmtpPort", ("587", "SMTP server port", "Notifications") },
                { "SmtpUseSsl", ("True", "Use TLS for SMTP delivery", "Notifications") },
                { "SmtpUser", ("", "SMTP account username", "Notifications") },
                { "SmtpPassword", ("", "Development placeholder only; configure securely before use", "Notifications") },
                { "SmtpFromEmail", ("", "Sender email address", "Notifications") },
                { "SmtpFromName", (ProductBrand.Name, "Sender display name", "Notifications") },
                { "OverdueSchedulerEnabled", ("False", "Enable periodic overdue processing", "Scheduler") },
                { "OverdueSchedulerRunOnStartup", ("False", "Run once after the configured startup delay", "Scheduler") },
                { "OverdueSchedulerIntervalMinutes", ("60", "Minutes between periodic scheduler runs", "Scheduler") },
                { "OverdueSchedulerInitialDelaySeconds", ("30", "Delay before an enabled startup run", "Scheduler") },
                { "OverdueSchedulerSendPendingEmails", ("False", "Allow scheduler runs to send pending email records", "Scheduler") },
                { "OverdueSchedulerMaxRunMinutes", ("10", "Maximum duration of one scheduler run", "Scheduler") },
                { "OverdueSchedulerLastRunAt", ("", "UTC timestamp of the most recent scheduler invocation", "Scheduler") },
                { "OverdueSchedulerLastSuccessAt", ("", "UTC timestamp of the most recent successful run", "Scheduler") },
                { "OverdueSchedulerLastFailureAt", ("", "UTC timestamp of the most recent failed run", "Scheduler") },
                { "OverdueSchedulerLastMessage", ("", "Summary of the most recent scheduler invocation", "Scheduler") },
                { "OverdueSchedulerIsRunning", ("False", "Persisted scheduler running indicator", "Scheduler") },
                { "InstituteName", ("Dr A Q Khan Institute of Computer Sciences and Information Technology KICSIT", "Name of the institution", "Institute") },
                { "InstituteAddress", ("KICSIT Campus, Kahuta, Pakistan", "Physical address", "Institute") },
                { "ReportHeader", (ProductBrand.Name.ToUpperInvariant(), "Header displayed on printouts", "Reports") },
                { "ReportFooter", ($"Generated by {ProductBrand.Name}", "Footer displayed on printouts", "Reports") },
                { "AllowedFileSize", ("10", "Maximum allowed size in MB for uploads", "Security") },
                { "DatabaseProvider", ("Sqlite", "Database provider: SqlServer or Sqlite", "Database") },
                { "RuntimeDataRoot", ("", "Optional release data root; empty uses the current storage mode default", "Runtime") },
                { "RuntimeStorageMode", ("Development", "Runtime storage mode: Development or Release", "Runtime") },
                { "UseReleaseDataRoot", ("False", "Use release-safe data root for database and runtime folders", "Runtime") },
                { "DatabaseFileName", ("KicsitLibrary.db", "SQLite database file name; do not rename without a data migration plan", "Runtime") },
                { "DocumentsFolderName", ("Documents", "Runtime documents folder name under the data root", "Runtime") },
                { "BackupsFolderName", ("Backups", "Runtime backups folder name under the data root", "Runtime") },
                { "ReportsFolderName", ("Reports", "Runtime reports folder name under the data root", "Runtime") },
                { "CertificatesFolderName", ("Certificates", "Runtime certificates folder name under the data root", "Runtime") },
                { "RestoreStagingFolderName", ("RestoreStaging", "Runtime restore staging folder name under the data root", "Runtime") },
                { "LogsFolderName", ("Logs", "Runtime logs folder name under the data root", "Runtime") },
                { "TempFolderName", ("Temp", "Runtime temporary folder name under the data root", "Runtime") },
                { "LocksFolderName", ("Locks", "Runtime ownership lock folder name under the data root", "Runtime") },
                { "LocalBackupFolder", ("C:\\KicsitLibraryBackup", "Legacy backup storage setting; not used by Priority 8A", "System") },
                { "BackupDefaultFolder", ("", $"Empty uses Documents\\{ProductBrand.BackupFolderName}", "Backup") },
                { "BackupCompressionEnabled", ("False", "Compress manual backups by default", "Backup") },
                { "BackupVerifyAfterCreation", ("True", "Verify manual backups after creation", "Backup") },
                { "BackupRetentionDays", ("30", "Future retention policy; automatic deletion is not implemented", "Backup") },
                { "BackupMaxHistoryRows", ("500", "Maximum backup history rows returned by default", "Backup") },
                { "AutomaticBackupEnabled", ("False", "Enable periodic automatic SQLite backups", "AutomaticBackup") },
                { "AutomaticBackupRunOnStartup", ("False", "Run one automatic backup after startup delay", "AutomaticBackup") },
                { "AutomaticBackupIntervalHours", ("24", "Hours between automatic backup runs", "AutomaticBackup") },
                { "AutomaticBackupInitialDelaySeconds", ("60", "Delay before an enabled startup backup", "AutomaticBackup") },
                { "AutomaticBackupCompress", ("False", "Create a ZIP for automatic backups", "AutomaticBackup") },
                { "AutomaticBackupVerifyAfterCreation", ("True", "Verify automatic backups after creation", "AutomaticBackup") },
                { "AutomaticBackupDestinationFolder", ("", $"Empty uses Documents\\{ProductBrand.BackupFolderName}", "AutomaticBackup") },
                { "AutomaticBackupRetentionEnabled", ("False", "Enable automatic backup retention", "AutomaticBackup") },
                { "AutomaticBackupRetentionDays", ("30", "Age threshold for automatic backup retention", "AutomaticBackup") },
                { "AutomaticBackupMaxHistoryRows", ("500", "Maximum active backup history rows retained", "AutomaticBackup") },
                { "AutomaticBackupDeletePhysicalFiles", ("False", "Allow retention to delete protected, validated linked files", "AutomaticBackup") },
                { "AutomaticBackupLastRunAt", ("", "UTC timestamp of the most recent automatic backup invocation", "AutomaticBackup") },
                { "AutomaticBackupLastSuccessAt", ("", "UTC timestamp of the most recent successful automatic backup", "AutomaticBackup") },
                { "AutomaticBackupLastFailureAt", ("", "UTC timestamp of the most recent failed automatic backup", "AutomaticBackup") },
                { "AutomaticBackupLastMessage", ("", "Summary of the most recent automatic backup invocation", "AutomaticBackup") },
                { "AutomaticBackupIsRunning", ("False", "Persisted automatic backup running indicator", "AutomaticBackup") },
                { "SingleInstanceMode", ("True", "Block multiple simultaneous desktop application instances", "System") },
                { "CriticalOperationLockTimeoutSeconds", ("15", "Seconds to wait before timing out a critical operation lock", "System") },
                { "AllowReadOnlySecondInstance", ("False", "Allow a second instance to open in read-only mode if SingleInstanceMode is True", "System") },
                { "CleanupStaleLockFilesOnStartup", ("True", "Automatically clean up orphaned lock files during startup", "System") },
                { "LockFileRetentionMinutes", ("120", "Minutes before an inactive lock file is considered stale and can be cleaned", "System") },
                { "DocumentStorageRoot", ("", $"Empty uses Documents\\{ProductBrand.Name}\\Documents", "Documents") },
                { "DocumentMaxFileSizeMb", ("25", "Maximum uploaded document file size in MB", "Documents") },
                { "DocumentAllowPhysicalDelete", ("False", "Future flag for physical deletion cleanup; soft delete remains default", "Documents") },
                { "DocumentAllowedExtensions", (".pdf,.docx,.xlsx,.jpg,.jpeg,.png", "Allowed document upload extensions", "Documents") }
            };

            foreach (var setting in defaultSettings)
            {
                if (!await context.SystemSettings.AnyAsync(s => s.Key == setting.Key))
                {
                    await context.SystemSettings.AddAsync(new SystemSettings
                    {
                        Key = setting.Key,
                        Value = setting.Value.Value,
                        Description = setting.Value.Description,
                        Group = setting.Value.Group
                    });
                }
            }

            var legacyBrandDefaults = new Dictionary<string, (string LegacyValue, string NewValue)>
            {
                ["SmtpFromName"] = ("KICSIT Library", ProductBrand.Name),
                ["ReportHeader"] = ("KICSIT LIBRARY MANAGEMENT SYSTEM", ProductBrand.Name.ToUpperInvariant()),
                ["ReportFooter"] = (
                    "Generated by KICSIT Library Management System",
                    $"Generated by {ProductBrand.Name}")
            };
            foreach (var legacyDefault in legacyBrandDefaults)
            {
                var current = await context.SystemSettings
                    .FirstOrDefaultAsync(setting => setting.Key == legacyDefault.Key);
                if (current?.Value == legacyDefault.Value.LegacyValue)
                {
                    current.Value = legacyDefault.Value.NewValue;
                }
            }
            await context.SaveChangesAsync();
        }
    }
}
