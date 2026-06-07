using System.Security.Cryptography;
using KicsitLibrary.Core;
using KicsitLibrary.Core.Interfaces;
using KicsitLibrary.Core.Models;
using KicsitLibrary.Data;
using Microsoft.EntityFrameworkCore;

namespace KicsitLibrary.Services.Documents;

public sealed class DocumentStorageService(KicsitLibraryDbContext context)
    : IDocumentStorageService
{
    private static readonly StringComparer PathComparer =
        OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;

    public async Task<DocumentStorageSettings> GetSettingsAsync(
        CancellationToken cancellationToken = default)
    {
        var values = await context.SystemSettings.AsNoTracking()
            .Where(setting => setting.Key == "DocumentStorageRoot" ||
                setting.Key == "DocumentMaxFileSizeMb" ||
                setting.Key == "DocumentAllowPhysicalDelete" ||
                setting.Key == "DocumentAllowedExtensions")
            .ToDictionaryAsync(
                setting => setting.Key,
                setting => setting.Value,
                cancellationToken);

        return new DocumentStorageSettings
        {
            StorageRoot = Read(values, "DocumentStorageRoot", string.Empty),
            MaxFileSizeMb = ReadInt(values, "DocumentMaxFileSizeMb", 25, 1, 250),
            AllowPhysicalDelete = ReadBool(values, "DocumentAllowPhysicalDelete", false),
            AllowedExtensions = Read(values, "DocumentAllowedExtensions",
                    ".pdf,.docx,.xlsx,.jpg,.jpeg,.png")
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(item => item.StartsWith('.') ? item.ToLowerInvariant() : $".{item.ToLowerInvariant()}")
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList()
        };
    }

    public string ResolveStorageRoot(DocumentStorageSettings settings)
    {
        var root = string.IsNullOrWhiteSpace(settings.StorageRoot)
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                ProductBrand.Name,
                "Documents")
            : settings.StorageRoot.Trim();
        root = Path.GetFullPath(root);
        Directory.CreateDirectory(root);
        EnsureNoReparsePoint(root, root);
        return root;
    }

    public string CreateStoredFilePath(
        DocumentStorageSettings settings,
        string documentType,
        string originalFileName)
    {
        var root = ResolveStorageRoot(settings);
        var typeFolder = SanitizePathPart(documentType);
        var monthFolder = DateTime.UtcNow.ToString("yyyy-MM");
        var folder = Path.Combine(root, typeFolder, monthFolder);
        Directory.CreateDirectory(folder);
        EnsureNoReparsePoint(folder, root);

        var extension = Path.GetExtension(originalFileName).ToLowerInvariant();
        var generatedName =
            $"{DateTime.UtcNow:yyyyMMddHHmmssfff}_{RandomNumberGenerator.GetHexString(12)}{extension}";
        var path = Path.GetFullPath(Path.Combine(folder, generatedName));
        if (!IsPathUnderStorageRoot(path, settings))
        {
            throw new InvalidOperationException(
                "The generated document path is outside the configured storage root.");
        }

        for (var attempt = 0; File.Exists(path); attempt++)
        {
            generatedName =
                $"{DateTime.UtcNow:yyyyMMddHHmmssfff}_{RandomNumberGenerator.GetHexString(12)}_{attempt + 2}{extension}";
            path = Path.GetFullPath(Path.Combine(folder, generatedName));
        }

        return path;
    }

    public bool IsPathUnderStorageRoot(
        string path,
        DocumentStorageSettings settings)
    {
        var root = Path.TrimEndingDirectorySeparator(ResolveStorageRoot(settings));
        var normalized = Path.GetFullPath(path);
        return PathComparer.Equals(normalized, root) ||
            normalized.StartsWith(
                root + Path.DirectorySeparatorChar,
                OperatingSystem.IsWindows()
                    ? StringComparison.OrdinalIgnoreCase
                    : StringComparison.Ordinal);
    }

    private static void EnsureNoReparsePoint(string path, string root)
    {
        var normalizedRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        var current = new DirectoryInfo(Path.GetFullPath(path));
        while (current.Exists)
        {
            if (current.Attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                throw new InvalidOperationException(
                    "Document storage paths must not contain symbolic links or reparse points.");
            }
            if (PathComparer.Equals(
                    Path.TrimEndingDirectorySeparator(current.FullName),
                    normalizedRoot))
            {
                break;
            }
            if (current.Parent == null)
            {
                break;
            }
            current = current.Parent;
        }
    }

    private static string SanitizePathPart(string value)
    {
        var invalid = Path.GetInvalidFileNameChars().ToHashSet();
        var sanitized = new string((string.IsNullOrWhiteSpace(value) ? "GeneralDocument" : value)
            .Select(character => invalid.Contains(character) || char.IsWhiteSpace(character)
                ? '-'
                : character)
            .ToArray()).Trim('-', '.');
        return string.IsNullOrWhiteSpace(sanitized) ? "GeneralDocument" : sanitized;
    }

    private static string Read(
        IReadOnlyDictionary<string, string> values,
        string key,
        string fallback) =>
        values.TryGetValue(key, out var value) ? value : fallback;

    private static int ReadInt(
        IReadOnlyDictionary<string, string> values,
        string key,
        int fallback,
        int minimum,
        int maximum) =>
        values.TryGetValue(key, out var value) && int.TryParse(value, out var parsed)
            ? Math.Clamp(parsed, minimum, maximum)
            : fallback;

    private static bool ReadBool(
        IReadOnlyDictionary<string, string> values,
        string key,
        bool fallback) =>
        values.TryGetValue(key, out var value) && bool.TryParse(value, out var parsed)
            ? parsed
            : fallback;
}
