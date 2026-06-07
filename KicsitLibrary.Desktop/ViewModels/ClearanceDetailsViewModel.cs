using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using KicsitLibrary.Core.Entities;
using KicsitLibrary.Core.Enums;
using KicsitLibrary.Core.Interfaces;
using KicsitLibrary.Core.Models;
using KicsitLibrary.Data;
using Microsoft.EntityFrameworkCore;

namespace KicsitLibrary.Desktop.ViewModels;

public partial class ClearanceDetailsViewModel(
    KicsitLibraryDbContext context,
    IClearanceService clearanceService) : ObservableObject
{
    public ObservableCollection<IssueRecord> BorrowingHistory { get; } = [];
    public ObservableCollection<ClearanceHistoryItem> ClearanceHistory { get; } = [];

    [ObservableProperty] private ClearanceCheckResult? _checkResult;
    [ObservableProperty] private string _title = "Clearance Details";
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _errorMessage = string.Empty;

    public async Task LoadAsync(MemberType memberType, int memberId)
    {
        IsBusy = true;
        ErrorMessage = string.Empty;
        try
        {
            CheckResult = memberType == MemberType.Student
                ? await clearanceService.CheckStudentClearanceAsync(memberId)
                : await clearanceService.CheckFacultyStaffClearanceAsync(memberId);
            Title = $"{CheckResult.MemberName} - Clearance Details";
            var issues = await context.IssueRecords.AsNoTracking()
                .Include(issue => issue.BookCopy).ThenInclude(copy => copy.BookMaster)
                .Include(issue => issue.ReceiveRecord)
                .Include(issue => issue.Fine)
                .Where(issue => memberType == MemberType.Student
                    ? issue.StudentId == memberId
                    : issue.FacultyStaffId == memberId)
                .OrderByDescending(issue => issue.IssueDate)
                .ToListAsync();
            BorrowingHistory.Clear();
            foreach (var issue in issues) BorrowingHistory.Add(issue);

            var history = await clearanceService.GetClearanceHistoryAsync();
            ClearanceHistory.Clear();
            foreach (var item in history.Where(item =>
                         item.MemberType == memberType && item.MemberId == memberId))
                ClearanceHistory.Add(item);
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally { IsBusy = false; }
    }
}
