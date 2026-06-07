using KicsitLibrary.Core.Models;

namespace KicsitLibrary.Core.Interfaces;

public interface IDocumentStorageService
{
    Task<DocumentStorageSettings> GetSettingsAsync(
        CancellationToken cancellationToken = default);

    string ResolveStorageRoot(DocumentStorageSettings settings);

    string CreateStoredFilePath(
        DocumentStorageSettings settings,
        string documentType,
        string originalFileName);

    bool IsPathUnderStorageRoot(
        string path,
        DocumentStorageSettings settings);
}
