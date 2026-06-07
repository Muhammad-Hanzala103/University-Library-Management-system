using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KicsitLibrary.Core.Entities;
using KicsitLibrary.Core.Enums;
using KicsitLibrary.Core.Interfaces;
using KicsitLibrary.Core.Models;
using KicsitLibrary.Data;
using KicsitLibrary.Desktop.Services;
using Microsoft.EntityFrameworkCore;

namespace KicsitLibrary.Desktop.ViewModels;

public partial class FacultyStaffClearanceViewModel : ObservableObject
{
    private readonly KicsitLibraryDbContext _context;
    private readonly IClearanceService _clearanceService;
    private readonly IClearanceDetailsDialogService _detailsDialog;

    public ObservableCollection<FacultyStaff> Members { get; } = [];
    public IReadOnlyList<string> StatusOptions { get; } =
        ["", .. Enum.GetNames<ClearanceStatus>()];
    public IReadOnlyList<string> FacultyTypeOptions { get; } =
        ["", .. Enum.GetNames<FacultyType>()];

    [ObservableProperty] private FacultyStaff? _selectedMember;
    [ObservableProperty] private ClearanceCheckResult? _checkResult;
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private string _department = string.Empty;
    [ObservableProperty] private string _selectedFacultyType = string.Empty;
    [ObservableProperty] private string _selectedStatus = string.Empty;
    [ObservableProperty] private string _remarks = string.Empty;
    [ObservableProperty] private string _revokeReason = string.Empty;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private string _errorMessage = string.Empty;
    [ObservableProperty] private bool _isBusy;

    public FacultyStaffClearanceViewModel(
        KicsitLibraryDbContext context,
        IClearanceService clearanceService,
        IClearanceDetailsDialogService detailsDialog)
    {
        _context = context;
        _clearanceService = clearanceService;
        _detailsDialog = detailsDialog;
        _ = RefreshAsync();
    }

    partial void OnSelectedMemberChanged(FacultyStaff? value)
    {
        CheckResult = null;
        ErrorMessage = string.Empty;
    }

    [RelayCommand]
    public async Task RefreshAsync()
    {
        IsBusy = true;
        ErrorMessage = string.Empty;
        try
        {
            var query = _context.FacultyStaff.AsNoTracking().AsQueryable();
            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                var search = SearchText.Trim();
                query = query.Where(member =>
                    member.PersonnelNumber.Contains(search) ||
                    member.Name.Contains(search));
            }
            if (!string.IsNullOrWhiteSpace(Department))
                query = query.Where(member => member.Department.Contains(Department));
            if (Enum.TryParse<FacultyType>(SelectedFacultyType, out var facultyType))
                query = query.Where(member => member.FacultyType == facultyType);
            if (Enum.TryParse<ClearanceStatus>(SelectedStatus, out var status))
                query = query.Where(member => member.ClearanceStatus == status);

            var members = await query.OrderBy(member => member.PersonnelNumber).ToListAsync();
            Members.Clear();
            foreach (var member in members) Members.Add(member);
            StatusMessage = $"{members.Count} faculty/staff member(s) loaded.";
        }
        catch (Exception ex) { ErrorMessage = $"Unable to load faculty/staff: {ex.Message}"; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task ClearFiltersAsync()
    {
        SearchText = Department = SelectedFacultyType = SelectedStatus = string.Empty;
        await RefreshAsync();
    }

    [RelayCommand]
    private async Task CheckClearanceAsync()
    {
        if (SelectedMember == null) { ErrorMessage = "Select a faculty/staff member first."; return; }
        await RunAsync(async () =>
        {
            CheckResult = await _clearanceService.CheckFacultyStaffClearanceAsync(SelectedMember.Id);
            StatusMessage = CheckResult.Message;
        });
    }

    [RelayCommand]
    private async Task ApproveClearanceAsync()
    {
        if (SelectedMember == null) { ErrorMessage = "Select a faculty/staff member first."; return; }
        await RunActionAsync(() =>
            _clearanceService.ApproveFacultyStaffClearanceAsync(SelectedMember.Id, Remarks));
    }

    [RelayCommand]
    private async Task RevokeClearanceAsync()
    {
        if (SelectedMember == null) { ErrorMessage = "Select a faculty/staff member first."; return; }
        await RunActionAsync(() =>
            _clearanceService.RevokeFacultyStaffClearanceAsync(SelectedMember.Id, RevokeReason));
    }

    [RelayCommand]
    private async Task GenerateCertificateAsync()
    {
        if (SelectedMember == null) { ErrorMessage = "Select a faculty/staff member first."; return; }
        await RunActionAsync(() => _clearanceService.GenerateClearanceCertificateAsync(
            MemberType.FacultyStaff, SelectedMember.Id));
    }

    [RelayCommand]
    private Task OpenBorrowingHistoryAsync()
    {
        return SelectedMember == null
            ? Task.CompletedTask
            : _detailsDialog.ShowAsync(MemberType.FacultyStaff, SelectedMember.Id);
    }

    private async Task RunActionAsync(Func<Task<ClearanceActionResult>> action)
    {
        await RunAsync(async () =>
        {
            var result = await action();
            if (!result.Succeeded)
            {
                ErrorMessage = result.ErrorMessage ?? result.Message;
                CheckResult = result.CheckResult;
                return;
            }
            StatusMessage = result.Message;
            await RefreshAsync();
            if (SelectedMember != null)
                CheckResult = await _clearanceService.CheckFacultyStaffClearanceAsync(SelectedMember.Id);
        });
    }

    private async Task RunAsync(Func<Task> action)
    {
        IsBusy = true;
        ErrorMessage = string.Empty;
        try { await action(); }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally { IsBusy = false; }
    }
}
