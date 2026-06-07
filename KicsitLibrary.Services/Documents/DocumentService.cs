using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using KicsitLibrary.Core.Entities;
using KicsitLibrary.Core.Enums;
using KicsitLibrary.Core.Interfaces;
using KicsitLibrary.Core.Models;
using KicsitLibrary.Data;
using KicsitLibrary.Services.Notifications;
using Microsoft.EntityFrameworkCore;

namespace KicsitLibrary.Services.Documents;

public sealed class DocumentService(
    KicsitLibraryDbContext context,
    IAuthenticationService authenticationService,
    IDocumentStorageService storageService) : IDocumentService
{
    private static readonly HashSet<string> BlockedExtensions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ".exe", ".bat", ".cmd", ".ps1", ".vbs", ".js",
            ".msi", ".dll", ".scr", ".com", ".jar"
        };

    public async Task<DocumentValidationResult> ValidateDocumentAsync(
        string sourceFilePath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sourceFilePath))
        {
            return ValidationFailure("Source file path is required.");
        }
        if (!File.Exists(sourceFilePath))
        {
            return ValidationFailure("Source file was not found.");
        }

        var fullPath = Path.GetFullPath(sourceFilePath);
        var fileInfo = new FileInfo(fullPath);
        var extension = Path.GetExtension(fileInfo.Name).ToLowerInvariant();
        var settings = await storageService.GetSettingsAsync(cancellationToken);
        if (BlockedExtensions.Contains(extension))
        {
            return ValidationFailure("Executable or script files are not allowed.", fileInfo.Name, extension);
        }
        if (!settings.AllowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            return ValidationFailure("This document extension is not allowed.", fileInfo.Name, extension);
        }
        var maxBytes = settings.MaxFileSizeMb * 1024L * 1024L;
        if (fileInfo.Length > maxBytes)
        {
            return ValidationFailure(
                $"Document exceeds the configured {settings.MaxFileSizeMb} MB size limit.",
                fileInfo.Name,
                extension,
                fileInfo.Length);
        }

        var signature = await ValidateSignatureAsync(fullPath, extension, cancellationToken);
        if (!signature.Succeeded)
        {
            signature.OriginalFileName = fileInfo.Name;
            signature.Extension = extension;
            signature.FileSizeBytes = fileInfo.Length;
            return signature;
        }

        return new DocumentValidationResult
        {
            Succeeded = true,
            OriginalFileName = fileInfo.Name,
            Extension = extension,
            DetectedContentType = signature.DetectedContentType,
            FileSizeBytes = fileInfo.Length,
            Sha256 = await ComputeSha256Async(fullPath, cancellationToken),
            Message = "Document validation passed."
        };
    }

    public async Task<DocumentUploadResult> UploadDocumentAsync(
        DocumentUploadRequest request,
        CancellationToken cancellationToken = default)
    {
        request ??= new DocumentUploadRequest();
        if (!CanUpload())
        {
            await LogAsync(
                "Document Upload Blocked",
                "Outcome=Blocked;Reason=Unauthorized upload attempt.",
                cancellationToken);
            return UploadFailure("The current user cannot upload documents.");
        }
        if (string.IsNullOrWhiteSpace(request.DocumentTitle))
        {
            return UploadFailure("Document title is required.");
        }
        if (string.IsNullOrWhiteSpace(request.DocumentType))
        {
            return UploadFailure("Document type is required.");
        }
        if (!IsKnownDocumentType(request.DocumentType))
        {
            return UploadFailure("Document type is not supported.");
        }

        var validation = await ValidateDocumentAsync(
            request.SourceFilePath,
            cancellationToken);
        if (!validation.Succeeded)
        {
            return UploadFailure(validation.ErrorMessage ?? validation.Message);
        }

        var settings = await storageService.GetSettingsAsync(cancellationToken);
        var storedPath = storageService.CreateStoredFilePath(
            settings,
            request.DocumentType,
            validation.OriginalFileName);
        await using (var source = new FileStream(
                         request.SourceFilePath,
                         FileMode.Open,
                         FileAccess.Read,
                         FileShare.Read))
        await using (var destination = new FileStream(
                         storedPath,
                         FileMode.CreateNew,
                         FileAccess.Write,
                         FileShare.None))
        {
            await source.CopyToAsync(destination, cancellationToken);
        }

        var user = authenticationService.CurrentUser;
        var upload = new DocumentUpload
        {
            DocumentTitle = request.DocumentTitle.Trim(),
            DocumentType = request.DocumentType.Trim(),
            VersionNumber = string.IsNullOrWhiteSpace(request.VersionNumber)
                ? "1.0"
                : request.VersionNumber.Trim(),
            Description = EmptyToNull(request.Description),
            UploadDate = DateTime.UtcNow,
            UploadedByUserId = user?.Id != 0 && user != null
                ? user.Id
                : request.UploadedByUserId,
            UploadedBy = !string.IsNullOrWhiteSpace(user?.FullName)
                ? user!.FullName
                : request.UploadedByUserName,
            OriginalFileName = validation.OriginalFileName,
            StoredFileName = Path.GetFileName(storedPath),
            StoredFilePath = storedPath,
            FilePath = storedPath,
            FileExtension = validation.Extension,
            ContentType = validation.DetectedContentType,
            FileSizeBytes = validation.FileSizeBytes,
            FileSha256 = validation.Sha256,
            ActiveStatus = true,
            ExpiryDate = request.ExpiryDate,
            Remarks = EmptyToNull(request.Remarks),
            RelatedEntityType = request.RelatedEntityType.Trim(),
            RelatedEntityId = request.RelatedEntityId
        };
        context.DocumentUploads.Add(upload);
        await SaveAsync(cancellationToken);
        await LogAsync(
            "Document Uploaded",
            $"EntityName=DocumentUpload;EntityId={upload.Id};DocumentType={Safe(upload.DocumentType)};StoredFile={Safe(upload.StoredFileName)}",
            cancellationToken);

        return new DocumentUploadResult
        {
            Succeeded = true,
            DocumentUploadId = upload.Id,
            StoredFilePath = storedPath,
            StoredFileName = upload.StoredFileName,
            OriginalFileName = upload.OriginalFileName,
            FileSizeBytes = upload.FileSizeBytes,
            FileSha256 = upload.FileSha256,
            Message = "Document uploaded and recorded successfully."
        };
    }

    public async Task<IReadOnlyList<DocumentListItem>> GetDocumentsAsync(
        DocumentFilter filter,
        CancellationToken cancellationToken = default)
    {
        await RequireViewAsync(cancellationToken);
        filter ??= new DocumentFilter();
        var query = context.DocumentUploads
            .IgnoreQueryFilters()
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter.SearchText))
        {
            var search = filter.SearchText.Trim();
            query = query.Where(item =>
                item.DocumentTitle.Contains(search) ||
                item.DocumentType.Contains(search) ||
                item.OriginalFileName.Contains(search) ||
                item.UploadedBy.Contains(search) ||
                (item.Description != null && item.Description.Contains(search)));
        }
        if (!string.IsNullOrWhiteSpace(filter.DocumentType))
        {
            query = query.Where(item => item.DocumentType == filter.DocumentType);
        }
        if (!string.IsNullOrWhiteSpace(filter.UploadedBy))
        {
            query = query.Where(item => item.UploadedBy.Contains(filter.UploadedBy));
        }
        if (!string.IsNullOrWhiteSpace(filter.RelatedEntityType))
        {
            query = query.Where(item => item.RelatedEntityType == filter.RelatedEntityType);
        }
        if (filter.RelatedEntityId.HasValue)
        {
            query = query.Where(item => item.RelatedEntityId == filter.RelatedEntityId.Value);
        }
        if (filter.FromDate.HasValue)
        {
            query = query.Where(item => item.UploadDate >= filter.FromDate.Value.Date);
        }
        if (filter.ToDate.HasValue)
        {
            query = query.Where(item => item.UploadDate < filter.ToDate.Value.Date.AddDays(1));
        }
        if (filter.ExpiredOnly)
        {
            var now = DateTime.UtcNow.Date;
            query = query.Where(item => item.ExpiryDate != null && item.ExpiryDate.Value.Date < now);
        }
        if (!string.IsNullOrWhiteSpace(filter.ActiveStatus))
        {
            var active = filter.ActiveStatus.Equals("Active", StringComparison.OrdinalIgnoreCase);
            query = query.Where(item => item.ActiveStatus == active && item.IsDeleted == !active);
        }

        var documents = await query
            .OrderByDescending(item => item.UploadDate)
            .ThenByDescending(item => item.Id)
            .Take(Math.Clamp(filter.Limit <= 0 ? 500 : filter.Limit, 1, 5000))
            .ToListAsync(cancellationToken);
        if (filter.MissingFileOnly)
        {
            documents = documents
                .Where(item => !File.Exists(GetStoredPath(item)))
                .ToList();
        }

        return documents
            .Where(CanViewDocument)
            .Select(MapList)
            .ToList();
    }

    public async Task<DocumentDetails?> GetDocumentDetailsAsync(
        int documentUploadId,
        CancellationToken cancellationToken = default)
    {
        await RequireViewAsync(cancellationToken);
        var document = await context.DocumentUploads
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == documentUploadId, cancellationToken);
        return document == null || !CanViewDocument(document)
            ? null
            : MapDetails(document);
    }

    public async Task<DocumentDownloadResult> OpenDocumentAsync(
        int documentUploadId,
        CancellationToken cancellationToken = default)
    {
        var document = await GetDocumentForActionAsync(documentUploadId, cancellationToken);
        if (document == null)
        {
            return DownloadFailure(documentUploadId, "Document was not found.");
        }
        if (!CanOpen(document))
        {
            await LogAsync(
                "Document Open Blocked",
                $"EntityName=DocumentUpload;EntityId={document.Id};Outcome=Blocked;Reason=Unauthorized open attempt.",
                cancellationToken);
            return DownloadFailure(documentUploadId, "The current user cannot open this document.");
        }

        var path = GetStoredPath(document);
        if (!File.Exists(path))
        {
            return DownloadFailure(documentUploadId, "The stored document file is missing.");
        }
        await LogAsync(
            "Document Opened",
            $"EntityName=DocumentUpload;EntityId={document.Id};DocumentType={Safe(document.DocumentType)}",
            cancellationToken);
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            });
        }
        catch
        {
            // The service result still returns the safe stored path for callers that only need access validation.
        }

        return new DocumentDownloadResult
        {
            Succeeded = true,
            DocumentUploadId = document.Id,
            FilePath = path,
            FileName = document.OriginalFileName,
            Message = "Document open request completed."
        };
    }

    public async Task<DocumentDownloadResult> CopyDocumentToAsync(
        int documentUploadId,
        string destinationFolder,
        CancellationToken cancellationToken = default)
    {
        var document = await GetDocumentForActionAsync(documentUploadId, cancellationToken);
        if (document == null)
        {
            return DownloadFailure(documentUploadId, "Document was not found.");
        }
        if (!CanOpen(document))
        {
            await LogAsync(
                "Document Copy Blocked",
                $"EntityName=DocumentUpload;EntityId={document.Id};Outcome=Blocked;Reason=Unauthorized copy attempt.",
                cancellationToken);
            return DownloadFailure(documentUploadId, "The current user cannot copy this document.");
        }
        if (string.IsNullOrWhiteSpace(destinationFolder))
        {
            return DownloadFailure(documentUploadId, "Destination folder is required.");
        }

        var source = GetStoredPath(document);
        if (!File.Exists(source))
        {
            return DownloadFailure(documentUploadId, "The stored document file is missing.");
        }
        var folder = Path.GetFullPath(destinationFolder);
        Directory.CreateDirectory(folder);
        var destination = ResolveUniquePath(
            folder,
            SafeFileName(document.OriginalFileName, document.FileExtension));
        await FileCopyAsync(source, destination, cancellationToken);
        await LogAsync(
            "Document Copied",
            $"EntityName=DocumentUpload;EntityId={document.Id};FileName={Safe(Path.GetFileName(destination))}",
            cancellationToken);
        return new DocumentDownloadResult
        {
            Succeeded = true,
            DocumentUploadId = document.Id,
            FilePath = destination,
            FileName = Path.GetFileName(destination),
            Message = "Document copied successfully."
        };
    }

    public async Task<DocumentDeleteResult> SoftDeleteDocumentAsync(
        int documentUploadId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        if (!CanDeleteOrRestore())
        {
            await LogAsync(
                "Document Delete Blocked",
                $"EntityName=DocumentUpload;EntityId={documentUploadId};Outcome=Blocked;Reason=Unauthorized delete attempt.",
                cancellationToken);
            return DeleteFailure(documentUploadId, "The current user cannot delete documents.");
        }
        if (string.IsNullOrWhiteSpace(reason))
        {
            return DeleteFailure(documentUploadId, "A document deletion reason is required.");
        }

        var document = await context.DocumentUploads
            .FirstOrDefaultAsync(item => item.Id == documentUploadId, cancellationToken);
        if (document == null)
        {
            return DeleteFailure(documentUploadId, "Document was not found.");
        }

        document.ActiveStatus = false;
        document.IsDeleted = true;
        document.DeletedAt = DateTime.UtcNow;
        document.DeletedReason = reason.Trim();
        document.DeletedByUserId = authenticationService.CurrentUser?.Id;
        document.DeletedBy = authenticationService.CurrentUser?.FullName ?? string.Empty;
        await SaveAsync(cancellationToken);
        await LogAsync(
            "Document Deleted",
            $"EntityName=DocumentUpload;EntityId={document.Id};Reason={Safe(reason)}",
            cancellationToken);
        return new DocumentDeleteResult
        {
            Succeeded = true,
            DocumentUploadId = document.Id,
            Message = "Document record was soft-deleted. The physical file was not deleted."
        };
    }

    public async Task<DocumentDeleteResult> RestoreDocumentAsync(
        int documentUploadId,
        CancellationToken cancellationToken = default)
    {
        if (!CanDeleteOrRestore())
        {
            await LogAsync(
                "Document Restore Blocked",
                $"EntityName=DocumentUpload;EntityId={documentUploadId};Outcome=Blocked;Reason=Unauthorized restore attempt.",
                cancellationToken);
            return DeleteFailure(documentUploadId, "The current user cannot restore documents.");
        }

        var document = await context.DocumentUploads
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(item => item.Id == documentUploadId, cancellationToken);
        if (document == null)
        {
            return DeleteFailure(documentUploadId, "Document was not found.");
        }
        if (!File.Exists(GetStoredPath(document)))
        {
            return DeleteFailure(documentUploadId, "The stored document file is missing and cannot be restored.");
        }

        document.ActiveStatus = true;
        document.IsDeleted = false;
        document.DeletedAt = null;
        document.DeletedReason = null;
        document.DeletedByUserId = null;
        document.DeletedBy = string.Empty;
        await SaveAsync(cancellationToken);
        await LogAsync(
            "Document Restored",
            $"EntityName=DocumentUpload;EntityId={document.Id}",
            cancellationToken);
        return new DocumentDeleteResult
        {
            Succeeded = true,
            DocumentUploadId = document.Id,
            Message = "Document record was restored."
        };
    }

    public async Task<IReadOnlyList<DocumentTypeSummary>> GetDocumentSummaryAsync(
        CancellationToken cancellationToken = default)
    {
        await RequireViewAsync(cancellationToken);
        var rows = await context.DocumentUploads
            .IgnoreQueryFilters()
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        return rows
            .Where(CanViewDocument)
            .GroupBy(item => item.DocumentType)
            .Select(group => new DocumentTypeSummary
            {
                DocumentType = group.Key,
                ActiveCount = group.Count(item => item.ActiveStatus && !item.IsDeleted),
                InactiveCount = group.Count(item => !item.ActiveStatus || item.IsDeleted),
                MissingFileCount = group.Count(item => !File.Exists(GetStoredPath(item))),
                ExpiredCount = group.Count(item =>
                    item.ExpiryDate.HasValue &&
                    item.ExpiryDate.Value.Date < DateTime.UtcNow.Date)
            })
            .OrderBy(item => item.DocumentType)
            .ToList();
    }

    public Task<IReadOnlyList<DocumentListItem>> GetDocumentsForEntityAsync(
        string relatedEntityType,
        int relatedEntityId,
        CancellationToken cancellationToken = default) =>
        GetDocumentsAsync(
            new DocumentFilter
            {
                RelatedEntityType = relatedEntityType,
                RelatedEntityId = relatedEntityId,
                ActiveStatus = "Active"
            },
            cancellationToken);

    private async Task<DocumentValidationResult> ValidateSignatureAsync(
        string path,
        string extension,
        CancellationToken cancellationToken)
    {
        var header = new byte[Math.Min(16, new FileInfo(path).Length)];
        await using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            _ = await stream.ReadAsync(header, cancellationToken);
        }

        return extension switch
        {
            ".pdf" when StartsWith(header, "%PDF"u8.ToArray()) =>
                SignatureSuccess("application/pdf"),
            ".png" when StartsWith(header, [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]) =>
                SignatureSuccess("image/png"),
            ".jpg" or ".jpeg" when header.Length >= 2 && header[0] == 0xFF && header[1] == 0xD8 =>
                SignatureSuccess("image/jpeg"),
            ".docx" when await IsOfficeDocumentAsync(path, "word/", cancellationToken) =>
                SignatureSuccess("application/vnd.openxmlformats-officedocument.wordprocessingml.document"),
            ".xlsx" when await IsOfficeDocumentAsync(path, "xl/", cancellationToken) =>
                SignatureSuccess("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"),
            _ => ValidationFailure("Document file signature does not match its extension.")
        };
    }

    private static async Task<bool> IsOfficeDocumentAsync(
        string path,
        string expectedPrefix,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
            return archive.Entries.Any(entry =>
                entry.FullName.Equals("[Content_Types].xml", StringComparison.OrdinalIgnoreCase)) &&
                archive.Entries.Any(entry =>
                    entry.FullName.StartsWith(expectedPrefix, StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            cancellationToken.ThrowIfCancellationRequested();
            return false;
        }
    }

    private async Task<DocumentUpload?> GetDocumentForActionAsync(
        int documentUploadId,
        CancellationToken cancellationToken) =>
        await context.DocumentUploads
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(item => item.Id == documentUploadId, cancellationToken);

    private async Task RequireViewAsync(CancellationToken cancellationToken)
    {
        if (!CanViewMetadata())
        {
            await LogAsync(
                "Document View Blocked",
                "EntityName=DocumentUpload;Outcome=Blocked;Reason=Unauthorized view attempt.",
                cancellationToken);
            throw new UnauthorizedAccessException(
                "The current user cannot view document metadata.");
        }
    }

    private bool CanViewMetadata()
    {
        var user = authenticationService.CurrentUser;
        return HasRole("Super Admin", "Admin", "Librarian", "Auditor") ||
            HasPermission(user, "VIEW_DOCUMENTS");
    }

    private bool CanViewDocument(DocumentUpload document)
    {
        if (HasRole("Super Admin", "Admin", "Librarian"))
        {
            return true;
        }
        if (HasRole("Auditor"))
        {
            return document.DocumentType is "AuditEvidence" or "VisitEvidence";
        }
        return HasPermission(authenticationService.CurrentUser, "VIEW_DOCUMENTS");
    }

    private bool CanUpload() =>
        HasRole("Super Admin", "Admin", "Librarian") ||
        HasPermission(authenticationService.CurrentUser, "MANAGE_DOCUMENTS");

    private bool CanOpen(DocumentUpload document)
    {
        if (HasRole("Super Admin", "Admin", "Librarian"))
        {
            return true;
        }
        if (HasRole("Auditor"))
        {
            return document.DocumentType is "AuditEvidence" or "VisitEvidence";
        }
        return HasPermission(authenticationService.CurrentUser, "OPEN_DOCUMENTS");
    }

    private bool CanDeleteOrRestore() =>
        HasRole("Super Admin", "Admin") ||
        HasPermission(authenticationService.CurrentUser, "MANAGE_DOCUMENTS");

    private bool HasRole(params string[] roleNames)
    {
        var user = authenticationService.CurrentUser;
        return user?.UserRoles.Any(userRole =>
            roleNames.Contains(userRole.Role.Name, StringComparer.OrdinalIgnoreCase)) == true;
    }

    private static bool HasPermission(User? user, string permissionCode) =>
        user?.UserRoles.Any(userRole =>
            userRole.Role.RolePermissions.Any(rolePermission =>
                rolePermission.Permission.Code == permissionCode)) == true;

    private static bool IsKnownDocumentType(string documentType) =>
        Enum.GetNames<DocumentType>().Contains(documentType, StringComparer.OrdinalIgnoreCase);

    private static DocumentListItem MapList(DocumentUpload item) =>
        new()
        {
            DocumentUploadId = item.Id,
            DocumentTitle = item.DocumentTitle,
            DocumentType = item.DocumentType,
            VersionNumber = item.VersionNumber,
            OriginalFileName = item.OriginalFileName,
            StoredFileName = item.StoredFileName,
            FileSizeBytes = item.FileSizeBytes,
            UploadedBy = item.UploadedBy,
            UploadDate = item.UploadDate,
            ExpiryDate = item.ExpiryDate,
            ActiveStatus = item.ActiveStatus && !item.IsDeleted
                ? File.Exists(GetStoredPath(item)) ? "Active" : "Missing File"
                : "Inactive",
            RelatedEntityType = item.RelatedEntityType,
            RelatedEntityId = item.RelatedEntityId
        };

    private static DocumentDetails MapDetails(DocumentUpload item)
    {
        var exists = File.Exists(GetStoredPath(item));
        var list = MapList(item);
        return new DocumentDetails
        {
            DocumentUploadId = list.DocumentUploadId,
            DocumentTitle = list.DocumentTitle,
            DocumentType = list.DocumentType,
            VersionNumber = list.VersionNumber,
            OriginalFileName = list.OriginalFileName,
            StoredFileName = list.StoredFileName,
            FileSizeBytes = list.FileSizeBytes,
            UploadedBy = list.UploadedBy,
            UploadDate = list.UploadDate,
            ExpiryDate = list.ExpiryDate,
            ActiveStatus = list.ActiveStatus,
            RelatedEntityType = list.RelatedEntityType,
            RelatedEntityId = list.RelatedEntityId,
            Description = item.Description ?? string.Empty,
            FileExtension = item.FileExtension,
            ContentType = item.ContentType,
            FileSha256 = item.FileSha256,
            Remarks = item.Remarks ?? string.Empty,
            FileExists = exists,
            FileStatusMessage = exists
                ? "Stored file exists."
                : "Warning: stored document file is missing on disk.",
            CreatedAt = item.CreatedAt,
            UpdatedAt = item.UpdatedAt,
            DeletedAt = item.DeletedAt,
            DeletedBy = item.DeletedBy,
            DeletedReason = item.DeletedReason ?? string.Empty
        };
    }

    private static string GetStoredPath(DocumentUpload item) =>
        string.IsNullOrWhiteSpace(item.StoredFilePath)
            ? item.FilePath
            : item.StoredFilePath;

    private static async Task FileCopyAsync(
        string source,
        string destination,
        CancellationToken cancellationToken)
    {
        await using var sourceStream = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read);
        await using var destinationStream = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await sourceStream.CopyToAsync(destinationStream, cancellationToken);
    }

    private static string ResolveUniquePath(string folder, string fileName)
    {
        var safe = SafeFileName(fileName, Path.GetExtension(fileName));
        var path = Path.Combine(folder, safe);
        var name = Path.GetFileNameWithoutExtension(safe);
        var extension = Path.GetExtension(safe);
        for (var index = 2; File.Exists(path); index++)
        {
            path = Path.Combine(folder, $"{name}_{index}{extension}");
        }
        return path;
    }

    private static string SafeFileName(string fileName, string extension)
    {
        var invalid = Path.GetInvalidFileNameChars().ToHashSet();
        var name = Path.GetFileNameWithoutExtension(fileName);
        var sanitized = new string((string.IsNullOrWhiteSpace(name) ? "document" : name)
            .Select(character => invalid.Contains(character) || char.IsControl(character)
                ? '_'
                : character)
            .ToArray()).Trim('_', '.');
        return $"{(string.IsNullOrWhiteSpace(sanitized) ? "document" : sanitized)}{extension}";
    }

    private async Task SaveAsync(CancellationToken cancellationToken) =>
        await SqliteRetryPolicy.ExecuteAsync(
            token => context.SaveChangesAsync(token),
            cancellationToken);

    private async Task LogAsync(
        string action,
        string detail,
        CancellationToken cancellationToken)
    {
        context.ActivityLogs.Add(new ActivityLog
        {
            Action = action,
            UserId = authenticationService.CurrentUser?.Id,
            IpAddress = "127.0.0.1",
            Detail = detail
        });
        try
        {
            await SaveAsync(cancellationToken);
        }
        catch
        {
            // Authorization outcomes remain authoritative if logging fails.
        }
    }

    private static async Task<string> ComputeSha256Async(
        string filePath,
        CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken));
    }

    private static bool StartsWith(byte[] value, byte[] prefix) =>
        value.Length >= prefix.Length && prefix.Where((item, index) => value[index] == item).Count() == prefix.Length;

    private static DocumentValidationResult SignatureSuccess(string contentType) =>
        new()
        {
            Succeeded = true,
            DetectedContentType = contentType,
            Message = "Signature validation passed."
        };

    private static DocumentValidationResult ValidationFailure(
        string error,
        string originalFileName = "",
        string extension = "",
        long fileSize = 0) =>
        new()
        {
            OriginalFileName = originalFileName,
            Extension = extension,
            FileSizeBytes = fileSize,
            Message = "Document validation failed.",
            ErrorMessage = error
        };

    private static DocumentUploadResult UploadFailure(string error) =>
        new()
        {
            Message = "Document upload failed.",
            ErrorMessage = error
        };

    private static DocumentDownloadResult DownloadFailure(int id, string error) =>
        new()
        {
            DocumentUploadId = id,
            Message = "Document action failed.",
            ErrorMessage = error
        };

    private static DocumentDeleteResult DeleteFailure(int id, string error) =>
        new()
        {
            DocumentUploadId = id,
            Message = "Document action failed.",
            ErrorMessage = error
        };

    private static string? EmptyToNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string Safe(string value) =>
        value.Replace(";", ",", StringComparison.Ordinal)
            .Replace("=", "-", StringComparison.Ordinal)
            .ReplaceLineEndings(" ");
}
