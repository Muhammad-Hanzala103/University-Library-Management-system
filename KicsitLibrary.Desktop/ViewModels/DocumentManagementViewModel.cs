using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KicsitLibrary.Core.Enums;
using KicsitLibrary.Core.Interfaces;
using KicsitLibrary.Core.Models;
using KicsitLibrary.Desktop.Services;
using Microsoft.Win32;

namespace KicsitLibrary.Desktop.ViewModels;

public partial class DocumentManagementViewModel : ObservableObject
{
    private readonly IDocumentService _documentService;
    private readonly IDocumentDialogService _dialogService;

    public IReadOnlyList<string> DocumentTypeOptions { get; } =
        ["All", .. Enum.GetNames<DocumentType>()];

    public IReadOnlyList<string> ActiveStatusOptions { get; } =
        ["All", "Active", "Inactive"];

    [ObservableProperty] private ObservableCollection<DocumentListItem> _documents = [];
    [ObservableProperty] private ObservableCollection<DocumentTypeSummary> _summary = [];
    [ObservableProperty] private DocumentListItem? _selectedDocument;
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private string _selectedDocumentType = "All";
    [ObservableProperty] private string _selectedActiveStatus = "All";
    [ObservableProperty] private string _uploadedBy = string.Empty;
    [ObservableProperty] private DateTime? _fromDate;
    [ObservableProperty] private DateTime? _toDate;
    [ObservableProperty] private bool _expiredOnly;
    [ObservableProperty] private bool _missingFileOnly;
    [ObservableProperty] private string _relatedEntityType = string.Empty;
    [ObservableProperty] private string _relatedEntityIdText = string.Empty;
    [ObservableProperty] private string _deleteReason = string.Empty;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _isBusy;

    public DocumentManagementViewModel(
        IDocumentService documentService,
        IDocumentDialogService dialogService)
    {
        _documentService = documentService;
        _dialogService = dialogService;
    }

    [RelayCommand]
    public async Task RefreshAsync()
    {
        IsBusy = true;
        try
        {
            var filter = new DocumentFilter
            {
                SearchText = SearchText,
                DocumentType = SelectedDocumentType == "All" ? string.Empty : SelectedDocumentType,
                ActiveStatus = SelectedActiveStatus == "All" ? string.Empty : SelectedActiveStatus,
                UploadedBy = UploadedBy,
                FromDate = FromDate,
                ToDate = ToDate,
                ExpiredOnly = ExpiredOnly,
                MissingFileOnly = MissingFileOnly,
                RelatedEntityType = RelatedEntityType,
                RelatedEntityId = int.TryParse(RelatedEntityIdText, out var id) ? id : null
            };
            Documents = new ObservableCollection<DocumentListItem>(
                await _documentService.GetDocumentsAsync(filter));
            Summary = new ObservableCollection<DocumentTypeSummary>(
                await _documentService.GetDocumentSummaryAsync());
            StatusMessage = $"{Documents.Count} document(s) loaded.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Unable to load documents: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task UploadDocumentAsync()
    {
        if (await _dialogService.ShowUploadAsync())
        {
            await RefreshAsync();
        }
    }

    [RelayCommand]
    private async Task ViewDetailsAsync()
    {
        if (SelectedDocument == null)
        {
            StatusMessage = "Select a document first.";
            return;
        }

        await _dialogService.ShowDetailsAsync(SelectedDocument.DocumentUploadId);
    }

    [RelayCommand]
    private async Task OpenDocumentAsync()
    {
        if (SelectedDocument == null)
        {
            StatusMessage = "Select a document first.";
            return;
        }

        var result = await _documentService.OpenDocumentAsync(SelectedDocument.DocumentUploadId);
        StatusMessage = result.Succeeded
            ? result.Message
            : result.ErrorMessage ?? result.Message;
    }

    [RelayCommand]
    private async Task CopyToAsync()
    {
        if (SelectedDocument == null)
        {
            StatusMessage = "Select a document first.";
            return;
        }

        var dialog = new SaveFileDialog
        {
            Title = "Choose document copy destination",
            FileName = SelectedDocument.OriginalFileName,
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

        var result = await _documentService.CopyDocumentToAsync(
            SelectedDocument.DocumentUploadId,
            folder);
        StatusMessage = result.Succeeded
            ? $"{result.Message} Copied as {result.FileName}."
            : result.ErrorMessage ?? result.Message;
    }

    [RelayCommand]
    private async Task DeleteSelectedAsync()
    {
        if (SelectedDocument == null)
        {
            StatusMessage = "Select a document first.";
            return;
        }

        var result = await _documentService.SoftDeleteDocumentAsync(
            SelectedDocument.DocumentUploadId,
            DeleteReason);
        StatusMessage = result.Succeeded
            ? result.Message
            : result.ErrorMessage ?? result.Message;
        if (result.Succeeded)
        {
            DeleteReason = string.Empty;
            await RefreshAsync();
        }
    }

    [RelayCommand]
    private async Task RestoreSelectedAsync()
    {
        if (SelectedDocument == null)
        {
            StatusMessage = "Select a document first.";
            return;
        }

        var result = await _documentService.RestoreDocumentAsync(
            SelectedDocument.DocumentUploadId);
        StatusMessage = result.Succeeded
            ? result.Message
            : result.ErrorMessage ?? result.Message;
        if (result.Succeeded)
        {
            await RefreshAsync();
        }
    }

    [RelayCommand]
    private void ClearFilters()
    {
        SearchText = string.Empty;
        SelectedDocumentType = "All";
        SelectedActiveStatus = "All";
        UploadedBy = string.Empty;
        FromDate = null;
        ToDate = null;
        ExpiredOnly = false;
        MissingFileOnly = false;
        RelatedEntityType = string.Empty;
        RelatedEntityIdText = string.Empty;
    }
}
