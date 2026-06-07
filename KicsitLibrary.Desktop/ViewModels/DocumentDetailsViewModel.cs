using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KicsitLibrary.Core.Interfaces;
using KicsitLibrary.Core.Models;
using Microsoft.Win32;

namespace KicsitLibrary.Desktop.ViewModels;

public partial class DocumentDetailsViewModel(IDocumentService documentService)
    : ObservableObject
{
    [ObservableProperty] private DocumentDetails? _details;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _isBusy;

    public async Task LoadAsync(int documentUploadId)
    {
        IsBusy = true;
        try
        {
            Details = await documentService.GetDocumentDetailsAsync(documentUploadId);
            StatusMessage = Details == null
                ? "Document details were not found or are not visible to the current user."
                : Details.FileStatusMessage;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Unable to load document details: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task OpenAsync()
    {
        if (Details == null)
        {
            StatusMessage = "Document details are not loaded.";
            return;
        }

        var result = await documentService.OpenDocumentAsync(Details.DocumentUploadId);
        StatusMessage = result.Succeeded
            ? result.Message
            : result.ErrorMessage ?? result.Message;
    }

    [RelayCommand]
    private async Task CopyToAsync()
    {
        if (Details == null)
        {
            StatusMessage = "Document details are not loaded.";
            return;
        }

        var dialog = new SaveFileDialog
        {
            Title = "Choose document copy destination",
            FileName = Details.OriginalFileName,
            Filter = "All files (*.*)|*.*",
            OverwritePrompt = false
        };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var folder = Path.GetDirectoryName(dialog.FileName);
        if (string.IsNullOrWhiteSpace(folder))
        {
            StatusMessage = "Destination folder is required.";
            return;
        }

        var result = await documentService.CopyDocumentToAsync(
            Details.DocumentUploadId,
            folder);
        StatusMessage = result.Succeeded
            ? $"{result.Message} Copied as {result.FileName}."
            : result.ErrorMessage ?? result.Message;
    }
}
