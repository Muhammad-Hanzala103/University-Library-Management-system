using KicsitLibrary.Core.Models;

namespace KicsitLibrary.Core.Interfaces;

public interface IDocumentService
{
    Task<DocumentValidationResult> ValidateDocumentAsync(
        string sourceFilePath,
        CancellationToken cancellationToken = default);

    Task<DocumentUploadResult> UploadDocumentAsync(
        DocumentUploadRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DocumentListItem>> GetDocumentsAsync(
        DocumentFilter filter,
        CancellationToken cancellationToken = default);

    Task<DocumentDetails?> GetDocumentDetailsAsync(
        int documentUploadId,
        CancellationToken cancellationToken = default);

    Task<DocumentDownloadResult> OpenDocumentAsync(
        int documentUploadId,
        CancellationToken cancellationToken = default);

    Task<DocumentDownloadResult> CopyDocumentToAsync(
        int documentUploadId,
        string destinationFolder,
        CancellationToken cancellationToken = default);

    Task<DocumentDeleteResult> SoftDeleteDocumentAsync(
        int documentUploadId,
        string reason,
        CancellationToken cancellationToken = default);

    Task<DocumentDeleteResult> RestoreDocumentAsync(
        int documentUploadId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DocumentTypeSummary>> GetDocumentSummaryAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DocumentListItem>> GetDocumentsForEntityAsync(
        string relatedEntityType,
        int relatedEntityId,
        CancellationToken cancellationToken = default);
}
