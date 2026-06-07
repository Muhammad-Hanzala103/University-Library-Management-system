using System.Security.Cryptography;
using KicsitLibrary.Core.Entities;
using KicsitLibrary.Core.Interfaces;
using KicsitLibrary.Core.Models;
using KicsitLibrary.Reports.Providers;
using KicsitLibrary.Services.Documents;
using KicsitLibrary.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace KicsitLibrary.Tests;

public class DocumentWorkflowTests
{
    [Fact]
    public async Task ValidPdf_UploadsSuccessfully()
    {
        await using var environment = await DocumentTestEnvironment.CreateAsync();
        var source = await environment.CreatePdfAsync("valid.pdf");

        var result = await environment.Service.UploadDocumentAsync(
            environment.Request(source, "LibrarySop"));

        Assert.True(result.Succeeded, result.ErrorMessage);
        Assert.True(File.Exists(result.StoredFilePath));
        Assert.Equal(".pdf", Path.GetExtension(result.StoredFileName));
    }

    [Fact]
    public async Task ValidPng_UploadsSuccessfully()
    {
        await using var environment = await DocumentTestEnvironment.CreateAsync();
        var source = await environment.CreatePngAsync("valid.png");

        var result = await environment.Service.UploadDocumentAsync(
            environment.Request(source, "GeneralDocument"));

        Assert.True(result.Succeeded, result.ErrorMessage);
        Assert.Equal("image/png", (await environment.Database.Context.DocumentUploads.SingleAsync()).ContentType);
    }

    [Fact]
    public async Task ExeUpload_IsRejected()
    {
        await using var environment = await DocumentTestEnvironment.CreateAsync();
        var source = await environment.CreateFileAsync("unsafe.exe", [0x4D, 0x5A, 0x00]);

        var result = await environment.Service.UploadDocumentAsync(
            environment.Request(source, "GeneralDocument"));

        Assert.False(result.Succeeded);
        Assert.Contains("not allowed", result.ErrorMessage);
        Assert.Empty(environment.StoredFiles());
    }

    [Fact]
    public async Task DisallowedExtension_IsRejected()
    {
        await using var environment = await DocumentTestEnvironment.CreateAsync();
        var source = await environment.CreateFileAsync("notes.txt", "plain text"u8.ToArray());

        var result = await environment.Service.UploadDocumentAsync(
            environment.Request(source, "GeneralDocument"));

        Assert.False(result.Succeeded);
        Assert.Contains("extension", result.ErrorMessage);
    }

    [Fact]
    public async Task OversizedFile_IsRejected()
    {
        await using var environment = await DocumentTestEnvironment.CreateAsync(maxFileSizeMb: 1);
        var source = Path.Combine(environment.SourceFolder, "large.pdf");
        await File.WriteAllBytesAsync(source, [.. "%PDF-1.7\n"u8.ToArray(), .. new byte[1024 * 1024]]);

        var result = await environment.Service.UploadDocumentAsync(
            environment.Request(source, "GeneralDocument"));

        Assert.False(result.Succeeded);
        Assert.Contains("size limit", result.ErrorMessage);
    }

    [Fact]
    public async Task StoredFilename_IsGeneratedAndSanitized()
    {
        await using var environment = await DocumentTestEnvironment.CreateAsync();
        var source = await environment.CreatePdfAsync("unsafe name ;=.pdf");

        var result = await environment.Service.UploadDocumentAsync(
            environment.Request(source, "GeneralDocument"));

        Assert.True(result.Succeeded, result.ErrorMessage);
        Assert.NotEqual(Path.GetFileName(source), result.StoredFileName);
        Assert.DoesNotContain(";", result.StoredFileName);
        Assert.DoesNotContain("=", result.StoredFileName);
    }

    [Fact]
    public async Task PathTraversalFilename_IsNeutralized()
    {
        await using var environment = await DocumentTestEnvironment.CreateAsync();
        var nested = Path.Combine(environment.SourceFolder, "nested");
        Directory.CreateDirectory(nested);
        var source = Path.Combine(nested, "..document.pdf");
        await File.WriteAllBytesAsync(source, "%PDF-1.4\n"u8.ToArray());

        var result = await environment.Service.UploadDocumentAsync(
            environment.Request(source, "AuditEvidence"));

        Assert.True(result.Succeeded, result.ErrorMessage);
        Assert.DoesNotContain("..", result.StoredFileName);
        Assert.StartsWith(environment.StorageRoot, result.StoredFilePath, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Sha256_IsStored()
    {
        await using var environment = await DocumentTestEnvironment.CreateAsync();
        var source = await environment.CreatePdfAsync("checksum.pdf");
        var expected = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(source)));

        var result = await environment.Service.UploadDocumentAsync(
            environment.Request(source, "LibraryPolicy"));
        var row = await environment.Database.Context.DocumentUploads.SingleAsync();

        Assert.True(result.Succeeded, result.ErrorMessage);
        Assert.Equal(expected, row.FileSha256);
        Assert.Equal(expected, result.FileSha256);
    }

    [Fact]
    public async Task File_IsStoredUnderConfiguredStorageRoot()
    {
        await using var environment = await DocumentTestEnvironment.CreateAsync();
        var source = await environment.CreatePdfAsync("rooted.pdf");

        var result = await environment.Service.UploadDocumentAsync(
            environment.Request(source, "GeneralDocument"));

        Assert.True(result.Succeeded, result.ErrorMessage);
        Assert.StartsWith(environment.StorageRoot, result.StoredFilePath, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Upload_WritesActivityLog()
    {
        await using var environment = await DocumentTestEnvironment.CreateAsync();
        var source = await environment.CreatePdfAsync("log.pdf");

        await environment.Service.UploadDocumentAsync(
            environment.Request(source, "GeneralDocument"));

        Assert.Contains(
            await environment.Database.Context.ActivityLogs.Select(item => item.Action).ToListAsync(),
            action => action == "Document Uploaded");
    }

    [Fact]
    public async Task GetDocuments_FiltersByDocumentType()
    {
        await using var environment = await DocumentTestEnvironment.CreateAsync();
        await environment.Service.UploadDocumentAsync(
            environment.Request(await environment.CreatePdfAsync("sop.pdf"), "LibrarySop"));
        await environment.Service.UploadDocumentAsync(
            environment.Request(await environment.CreatePdfAsync("policy.pdf"), "LibraryPolicy"));

        var documents = await environment.Service.GetDocumentsAsync(
            new DocumentFilter { DocumentType = "LibrarySop" });

        Assert.Single(documents);
        Assert.Equal("LibrarySop", documents[0].DocumentType);
    }

    [Fact]
    public async Task OpenDocument_BlocksUnauthorizedRole()
    {
        await using var environment = await DocumentTestEnvironment.CreateAsync();
        var upload = await environment.Service.UploadDocumentAsync(
            environment.Request(await environment.CreatePdfAsync("blocked.pdf"), "GeneralDocument"));
        var readOnly = environment.CreateServiceForRole("Read Only Viewer", "VIEW_DOCUMENTS");

        var result = await readOnly.OpenDocumentAsync(upload.DocumentUploadId!.Value);

        Assert.False(result.Succeeded);
        Assert.Contains("cannot open", result.ErrorMessage);
        Assert.Contains(
            await environment.Database.Context.ActivityLogs.Select(item => item.Action).ToListAsync(),
            action => action == "Document Open Blocked");
    }

    [Fact]
    public async Task SoftDelete_MarksInactiveAndLogs()
    {
        await using var environment = await DocumentTestEnvironment.CreateAsync();
        var upload = await environment.Service.UploadDocumentAsync(
            environment.Request(await environment.CreatePdfAsync("delete.pdf"), "GeneralDocument"));

        var result = await environment.Service.SoftDeleteDocumentAsync(
            upload.DocumentUploadId!.Value,
            "Superseded by new version.");
        var row = await environment.Database.Context.DocumentUploads
            .IgnoreQueryFilters()
            .SingleAsync();

        Assert.True(result.Succeeded, result.ErrorMessage);
        Assert.False(row.ActiveStatus);
        Assert.True(row.IsDeleted);
        Assert.Contains(
            await environment.Database.Context.ActivityLogs.Select(item => item.Action).ToListAsync(),
            action => action == "Document Deleted");
    }

    [Fact]
    public async Task Restore_RestoresInactiveDocument()
    {
        await using var environment = await DocumentTestEnvironment.CreateAsync();
        var upload = await environment.Service.UploadDocumentAsync(
            environment.Request(await environment.CreatePdfAsync("restore.pdf"), "GeneralDocument"));
        await environment.Service.SoftDeleteDocumentAsync(upload.DocumentUploadId!.Value, "Test restore.");

        var result = await environment.Service.RestoreDocumentAsync(upload.DocumentUploadId.Value);
        var row = await environment.Database.Context.DocumentUploads.SingleAsync();

        Assert.True(result.Succeeded, result.ErrorMessage);
        Assert.True(row.ActiveStatus);
        Assert.False(row.IsDeleted);
    }

    [Fact]
    public async Task MissingPhysicalFile_IsReportedClearly()
    {
        await using var environment = await DocumentTestEnvironment.CreateAsync();
        var upload = await environment.Service.UploadDocumentAsync(
            environment.Request(await environment.CreatePdfAsync("missing.pdf"), "GeneralDocument"));
        File.Delete(upload.StoredFilePath);

        var details = await environment.Service.GetDocumentDetailsAsync(upload.DocumentUploadId!.Value);

        Assert.NotNull(details);
        Assert.False(details.FileExists);
        Assert.Contains("missing", details.FileStatusMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Missing File", details.ActiveStatus);
    }

    [Fact]
    public async Task SopDocumentsReport_ReturnsUploadedSopRow()
    {
        await using var environment = await DocumentTestEnvironment.CreateAsync();
        await environment.Service.UploadDocumentAsync(
            environment.Request(await environment.CreatePdfAsync("sop-report.pdf"), "LibrarySop"));
        var provider = new SopDocumentsReportDataProvider(environment.Database.Context);

        var report = await provider.GenerateAsync([], "Test User");

        Assert.Equal("sop-documents", provider.Definition.Key);
        Assert.Single(report.Rows);
        Assert.Equal("LibrarySop", report.Rows[0]["Type"]);
    }

    [Fact]
    public async Task CopyDocumentTo_CopiesFileAndLogs()
    {
        await using var environment = await DocumentTestEnvironment.CreateAsync();
        var upload = await environment.Service.UploadDocumentAsync(
            environment.Request(await environment.CreatePdfAsync("copy.pdf"), "GeneralDocument"));
        var destinationFolder = Path.Combine(environment.WorkFolder, "Copies");

        var result = await environment.Service.CopyDocumentToAsync(
            upload.DocumentUploadId!.Value,
            destinationFolder);

        Assert.True(result.Succeeded, result.ErrorMessage);
        Assert.True(File.Exists(result.FilePath));
        Assert.Contains(
            await environment.Database.Context.ActivityLogs.Select(item => item.Action).ToListAsync(),
            action => action == "Document Copied");
    }

    private sealed class DocumentTestEnvironment : IAsyncDisposable
    {
        private DocumentTestEnvironment(
            SqliteTestDatabase database,
            DocumentService service,
            User user,
            string workFolder,
            string sourceFolder,
            string storageRoot)
        {
            Database = database;
            Service = service;
            User = user;
            WorkFolder = workFolder;
            SourceFolder = sourceFolder;
            StorageRoot = storageRoot;
        }

        public SqliteTestDatabase Database { get; }
        public DocumentService Service { get; }
        public User User { get; }
        public string WorkFolder { get; }
        public string SourceFolder { get; }
        public string StorageRoot { get; }

        public static async Task<DocumentTestEnvironment> CreateAsync(int maxFileSizeMb = 25)
        {
            var database = await SqliteTestDatabase.CreateAsync();
            var data = await database.AddCirculationDataAsync();
            data.User.FullName = "Document Administrator";
            data.User.UserRoles.Clear();
            data.User.UserRoles.Add(CreateUserRole("Admin",
                "VIEW_DOCUMENTS",
                "OPEN_DOCUMENTS",
                "MANAGE_DOCUMENTS"));
            await database.Context.SaveChangesAsync();

            var workFolder = Path.Combine(
                Path.GetTempPath(),
                "KicsitLibrary.Tests",
                "Documents",
                Guid.NewGuid().ToString("N"));
            var sourceFolder = Path.Combine(workFolder, "Sources");
            var storageRoot = Path.Combine(workFolder, "Storage");
            Directory.CreateDirectory(sourceFolder);
            Directory.CreateDirectory(storageRoot);
            await database.SetSystemSettingAsync("DocumentStorageRoot", storageRoot, "Documents");
            await database.SetSystemSettingAsync("DocumentMaxFileSizeMb", maxFileSizeMb.ToString(), "Documents");
            await database.SetSystemSettingAsync("DocumentAllowPhysicalDelete", "False", "Documents");
            await database.SetSystemSettingAsync("DocumentAllowedExtensions", ".pdf,.docx,.xlsx,.jpg,.jpeg,.png", "Documents");
            await database.SetSystemSettingAsync("InstituteName", "Test Institute", "General");

            return new DocumentTestEnvironment(
                database,
                new DocumentService(
                    database.Context,
                    new FakeAuthenticationService(data.User),
                    new DocumentStorageService(database.Context)),
                data.User,
                workFolder,
                sourceFolder,
                storageRoot);
        }

        public DocumentService CreateServiceForRole(string roleName, params string[] permissionCodes)
        {
            var user = new User
            {
                Id = User.Id,
                Username = User.Username,
                FullName = User.FullName,
                UserRoles =
                {
                    CreateUserRole(roleName, permissionCodes)
                }
            };
            return new DocumentService(
                Database.Context,
                new FakeAuthenticationService(user),
                new DocumentStorageService(Database.Context));
        }

        public DocumentUploadRequest Request(string sourceFilePath, string documentType) =>
            new()
            {
                DocumentTitle = $"Test {documentType}",
                DocumentType = documentType,
                SourceFilePath = sourceFilePath,
                UploadedByUserId = User.Id,
                UploadedByUserName = User.FullName,
                VersionNumber = "1.0",
                Description = "Test document upload.",
                Remarks = "Created by isolated test."
            };

        public async Task<string> CreatePdfAsync(string fileName) =>
            await CreateFileAsync(fileName, "%PDF-1.4\n% test document\n"u8.ToArray());

        public async Task<string> CreatePngAsync(string fileName) =>
            await CreateFileAsync(fileName, [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00]);

        public async Task<string> CreateFileAsync(string fileName, byte[] bytes)
        {
            var path = Path.Combine(SourceFolder, fileName);
            await File.WriteAllBytesAsync(path, bytes);
            return path;
        }

        public IReadOnlyList<string> StoredFiles() =>
            Directory.Exists(StorageRoot)
                ? Directory.GetFiles(StorageRoot, "*", SearchOption.AllDirectories)
                : [];

        public async ValueTask DisposeAsync()
        {
            await Database.DisposeAsync();
            if (Directory.Exists(WorkFolder))
            {
                Directory.Delete(WorkFolder, recursive: true);
            }
        }

        private static UserRole CreateUserRole(string roleName, params string[] permissionCodes) =>
            new()
            {
                Role = new Role
                {
                    Name = roleName,
                    RolePermissions = permissionCodes
                        .Select(code => new RolePermission
                        {
                            Permission = new Permission
                            {
                                Code = code,
                                Name = code
                            }
                        })
                        .ToList()
                }
            };
    }

    private sealed class FakeAuthenticationService(User currentUser) : IAuthenticationService
    {
        public User? CurrentUser { get; } = currentUser;
        public Task<User?> LoginAsync(string username, string password) =>
            Task.FromResult(CurrentUser);
        public Task<bool> ChangePasswordAsync(int userId, string oldPassword, string newPassword) =>
            Task.FromResult(false);
        public Task<bool> VerifyUserPermissionAsync(int userId, string permissionCode) =>
            Task.FromResult(CurrentUser?.UserRoles.Any(userRole =>
                userRole.Role.RolePermissions.Any(rolePermission =>
                    rolePermission.Permission.Code == permissionCode)) == true);
        public Task LogoutAsync() => Task.CompletedTask;
    }
}
