using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace KicsitLibrary.Desktop.ViewModels;

public partial class ClearanceDashboardViewModel : ObservableObject
{
    private readonly StudentClearanceViewModel _studentViewModel;
    private readonly FacultyStaffClearanceViewModel _facultyViewModel;

    [ObservableProperty] private object _currentClearanceView;

    public ClearanceDashboardViewModel(
        StudentClearanceViewModel studentViewModel,
        FacultyStaffClearanceViewModel facultyViewModel)
    {
        _studentViewModel = studentViewModel;
        _facultyViewModel = facultyViewModel;
        _currentClearanceView = _studentViewModel;
    }

    [RelayCommand]
    private Task ShowStudentsAsync()
    {
        CurrentClearanceView = _studentViewModel;
        return _studentViewModel.RefreshAsync();
    }

    [RelayCommand]
    private Task ShowFacultyAsync()
    {
        CurrentClearanceView = _facultyViewModel;
        return _facultyViewModel.RefreshAsync();
    }
}
