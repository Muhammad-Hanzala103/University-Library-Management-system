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

public partial class StudentClearanceViewModel : ObservableObject
{
    private readonly KicsitLibraryDbContext _context;
    private readonly IClearanceService _clearanceService;
    private readonly IClearanceDetailsDialogService _detailsDialog;

    public ObservableCollection<Student> Students { get; } = [];
    public IReadOnlyList<string> StatusOptions { get; } =
        ["", .. Enum.GetNames<ClearanceStatus>()];

    [ObservableProperty] private Student? _selectedStudent;
    [ObservableProperty] private ClearanceCheckResult? _checkResult;
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private string _department = string.Empty;
    [ObservableProperty] private string _program = string.Empty;
    [ObservableProperty] private string _batch = string.Empty;
    [ObservableProperty] private string _selectedStatus = string.Empty;
    [ObservableProperty] private string _remarks = string.Empty;
    [ObservableProperty] private string _revokeReason = string.Empty;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private string _errorMessage = string.Empty;
    [ObservableProperty] private bool _isBusy;

    public StudentClearanceViewModel(
        KicsitLibraryDbContext context,
        IClearanceService clearanceService,
        IClearanceDetailsDialogService detailsDialog)
    {
        _context = context;
        _clearanceService = clearanceService;
        _detailsDialog = detailsDialog;
        _ = RefreshAsync();
    }

    partial void OnSelectedStudentChanged(Student? value)
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
            var query = _context.Students.AsNoTracking().AsQueryable();
            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                var search = SearchText.Trim();
                query = query.Where(student =>
                    student.RegistrationNumber.Contains(search) ||
                    student.AdmissionNumber.Contains(search) ||
                    student.Name.Contains(search));
            }
            if (!string.IsNullOrWhiteSpace(Department))
                query = query.Where(student => student.Department.Contains(Department));
            if (!string.IsNullOrWhiteSpace(Program))
                query = query.Where(student => student.Program.Contains(Program));
            if (!string.IsNullOrWhiteSpace(Batch))
                query = query.Where(student => student.Batch.Contains(Batch));
            if (Enum.TryParse<ClearanceStatus>(SelectedStatus, out var status))
                query = query.Where(student => student.ClearanceStatus == status);

            var members = await query.OrderBy(student => student.RegistrationNumber).ToListAsync();
            Students.Clear();
            foreach (var member in members) Students.Add(member);
            StatusMessage = $"{members.Count} student(s) loaded.";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Unable to load students: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ClearFiltersAsync()
    {
        SearchText = Department = Program = Batch = SelectedStatus = string.Empty;
        await RefreshAsync();
    }

    [RelayCommand]
    private async Task CheckClearanceAsync()
    {
        if (SelectedStudent == null)
        {
            ErrorMessage = "Select a student first.";
            return;
        }
        await RunAsync(async () =>
        {
            CheckResult = await _clearanceService.CheckStudentClearanceAsync(SelectedStudent.Id);
            StatusMessage = CheckResult.Message;
        });
    }

    [RelayCommand]
    private async Task ApproveClearanceAsync()
    {
        if (SelectedStudent == null)
        {
            ErrorMessage = "Select a student first.";
            return;
        }
        await RunActionAsync(() =>
            _clearanceService.ApproveStudentClearanceAsync(SelectedStudent.Id, Remarks));
    }

    [RelayCommand]
    private async Task RevokeClearanceAsync()
    {
        if (SelectedStudent == null)
        {
            ErrorMessage = "Select a student first.";
            return;
        }
        await RunActionAsync(() =>
            _clearanceService.RevokeStudentClearanceAsync(SelectedStudent.Id, RevokeReason));
    }

    [RelayCommand]
    private async Task GenerateCertificateAsync()
    {
        if (SelectedStudent == null)
        {
            ErrorMessage = "Select a student first.";
            return;
        }
        await RunActionAsync(() => _clearanceService.GenerateClearanceCertificateAsync(
            MemberType.Student, SelectedStudent.Id));
    }

    [RelayCommand]
    private Task OpenBorrowingHistoryAsync()
    {
        return SelectedStudent == null
            ? Task.CompletedTask
            : _detailsDialog.ShowAsync(MemberType.Student, SelectedStudent.Id);
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
            if (SelectedStudent != null)
                CheckResult = await _clearanceService.CheckStudentClearanceAsync(SelectedStudent.Id);
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
