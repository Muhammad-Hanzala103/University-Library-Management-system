using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KicsitLibrary.Core.Entities;
using KicsitLibrary.Core.Enums;
using KicsitLibrary.Core.Interfaces;
using KicsitLibrary.Core.Models;
using KicsitLibrary.Data;
using Microsoft.EntityFrameworkCore;

namespace KicsitLibrary.Desktop.ViewModels;

public partial class ReservationFormViewModel : ObservableObject
{
    private readonly KicsitLibraryDbContext _context;
    private readonly IReservationService _reservationService;

    public event Action<bool?>? CloseRequested;
    public IReadOnlyList<MemberType> MemberTypes { get; } = Enum.GetValues<MemberType>();

    [ObservableProperty] private MemberType _selectedMemberType = MemberType.Student;
    [ObservableProperty] private string _memberSearchText = string.Empty;
    [ObservableProperty] private string _bookSearchText = string.Empty;
    [ObservableProperty] private ObservableCollection<Student> _students = [];
    [ObservableProperty] private ObservableCollection<FacultyStaff> _facultyStaff = [];
    [ObservableProperty] private ObservableCollection<BookMaster> _books = [];
    [ObservableProperty] private Student? _selectedStudent;
    [ObservableProperty] private FacultyStaff? _selectedFacultyStaff;
    [ObservableProperty] private BookMaster? _selectedBook;
    [ObservableProperty] private ReservationEligibilityResult? _eligibility;
    [ObservableProperty] private string _remarks = string.Empty;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _isBusy;

    public ReservationFormViewModel(
        KicsitLibraryDbContext context,
        IReservationService reservationService)
    {
        _context = context;
        _reservationService = reservationService;
    }

    public Task LoadAsync() => SearchAsync();

    [RelayCommand]
    private async Task SearchAsync()
    {
        IsBusy = true;
        try
        {
            var memberSearch = MemberSearchText.Trim();
            var bookSearch = BookSearchText.Trim();
            Students = new ObservableCollection<Student>(await _context.Students.AsNoTracking()
                .Where(item => memberSearch == string.Empty ||
                    item.Name.Contains(memberSearch) ||
                    item.RegistrationNumber.Contains(memberSearch))
                .OrderBy(item => item.Name)
                .Take(50)
                .ToListAsync());
            FacultyStaff = new ObservableCollection<FacultyStaff>(await _context.FacultyStaff.AsNoTracking()
                .Where(item => memberSearch == string.Empty ||
                    item.Name.Contains(memberSearch) ||
                    item.PersonnelNumber.Contains(memberSearch))
                .OrderBy(item => item.Name)
                .Take(50)
                .ToListAsync());
            Books = new ObservableCollection<BookMaster>(await _context.BookMasters.AsNoTracking()
                .Where(item => bookSearch == string.Empty ||
                    item.Title.Contains(bookSearch) ||
                    item.UniqueTitleNumber.Contains(bookSearch))
                .OrderBy(item => item.Title)
                .Take(50)
                .ToListAsync());
            StatusMessage = "Search results refreshed.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Search failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task CheckEligibilityAsync()
    {
        var memberId = GetSelectedMemberId();
        if (!memberId.HasValue || SelectedBook == null)
        {
            StatusMessage = "Select a member and book title first.";
            return;
        }

        Eligibility = await _reservationService.CheckReservationEligibilityAsync(
            SelectedBook.Id,
            memberId.Value,
            SelectedMemberType);
        StatusMessage = Eligibility.Message;
    }

    [RelayCommand]
    private async Task CreateAsync()
    {
        var memberId = GetSelectedMemberId();
        if (!memberId.HasValue || SelectedBook == null)
        {
            StatusMessage = "Select a member and book title first.";
            return;
        }

        var result = await _reservationService.CreateReservationAsync(
            SelectedBook.Id,
            memberId.Value,
            SelectedMemberType,
            Remarks);
        StatusMessage = result.Succeeded ? result.Message : result.ErrorMessage ?? result.Message;
        if (result.Succeeded)
            CloseRequested?.Invoke(true);
    }

    [RelayCommand]
    private void Cancel() => CloseRequested?.Invoke(false);

    partial void OnSelectedMemberTypeChanged(MemberType value)
    {
        Eligibility = null;
        StatusMessage = string.Empty;
    }

    private int? GetSelectedMemberId() =>
        SelectedMemberType == MemberType.Student
            ? SelectedStudent?.Id
            : SelectedFacultyStaff?.Id;
}
