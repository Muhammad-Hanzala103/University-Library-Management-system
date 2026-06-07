using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KicsitLibrary.Core.Enums;
using KicsitLibrary.Core.Interfaces;
using KicsitLibrary.Core.Models;
using Microsoft.Win32;

namespace KicsitLibrary.Desktop.ViewModels;

public partial class DocumentUploadViewModel(
    IDocumentService documentService,
    IAuthenticationService authenticationService) : ObservableObject
{
    public event Action<bool?>? CloseRequested;

    public IReadOnlyList<string> DocumentTypes { get; } =
        Enum.GetNames<DocumentType>();

    [ObservableProperty] private string _documentTitle = string.Empty;
    [ObservableProperty] private string _selectedDocumentType = nameof(DocumentType.GeneralDocument);
    [ObservableProperty] private string _versionNumber = "1.0";
    [ObservableProperty] private string _description = string.Empty;
    [ObservableProperty] private string _sourceFilePath = string.Empty;
    [ObservableProperty] private DateTime? _expiryDate;
    [ObservableProperty] private string _relatedEntityType = string.Empty;
    [ObservableProperty] private string _relatedEntityIdText = string.Empty;
    [ObservableProperty] private string _remarks = string.Empty;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _isBusy;

    [RelayCommand]
    private void BrowseFile()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select document",
            Filter = "Allowed documents (*.pdf;*.docx;*.xlsx;*.jpg;*.jpeg;*.png)|*.pdf;*.docx;*.xlsx;*.jpg;*.jpeg;*.png|All files (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false
        };
        if (dialog.ShowDialog() == true)
        {
            SourceFilePath = dialog.FileName;
        }
    }

    [RelayCommand]
    private async Task ValidateAsync()
    {
        IsBusy = true;
        try
        {
            var result = await documentService.ValidateDocumentAsync(SourceFilePath);
            StatusMessage = result.Succeeded
                ? $"{result.Message} {result.OriginalFileName} ({result.FileSizeBytes:N0} bytes)."
                : result.ErrorMessage ?? result.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task UploadAsync()
    {
        IsBusy = true;
        try
        {
            var user = authenticationService.CurrentUser;
            var result = await documentService.UploadDocumentAsync(new DocumentUploadRequest
            {
                DocumentTitle = DocumentTitle,
                DocumentType = SelectedDocumentType,
                Description = Description,
                SourceFilePath = SourceFilePath,
                UploadedByUserId = user?.Id ?? 0,
                UploadedByUserName = user?.FullName ?? "Unknown User",
                RelatedEntityType = RelatedEntityType,
                RelatedEntityId = int.TryParse(RelatedEntityIdText, out var id) ? id : null,
                VersionNumber = VersionNumber,
                ExpiryDate = ExpiryDate,
                Remarks = Remarks
            });
            StatusMessage = result.Succeeded
                ? result.Message
                : result.ErrorMessage ?? result.Message;
            if (result.Succeeded)
            {
                CloseRequested?.Invoke(true);
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void Cancel() => CloseRequested?.Invoke(false);
}
