using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KicsitLibrary.Core.Entities;
using KicsitLibrary.Core.Enums;
using KicsitLibrary.Core.Interfaces;

namespace KicsitLibrary.Desktop.ViewModels
{
    public partial class FacultyStaffManagementViewModel : ObservableObject
    {
        private readonly IConsumerService _consumerService;
        private readonly IAuthenticationService _authService;

        [ObservableProperty]
        private ObservableCollection<FacultyStaff> _facultyStaffMembers = new();

        // Filters lists
        public ObservableCollection<string> Departments { get; } = new() { "CS", "CE", "SE", "AI", "DS", "General" };
        public ObservableCollection<FacultyType> FacultyTypes { get; } = new() 
        { 
            FacultyType.PermanentFaculty, 
            FacultyType.VisitingFaculty, 
            FacultyType.Staff, 
            FacultyType.Guest 
        };

        // Search Filter Properties
        [ObservableProperty]
        private string _searchQuery = string.Empty;

        [ObservableProperty]
        private FacultyType? _selectedFacultyType;

        [ObservableProperty]
        private string? _selectedDepartment;

        [ObservableProperty]
        private bool? _selectedActiveStatus;

        [ObservableProperty]
        private string _errorMessage = string.Empty;

        [ObservableProperty]
        private bool _isBusy;

        public FacultyStaffManagementViewModel(IConsumerService consumerService, IAuthenticationService authService)
        {
            _consumerService = consumerService ?? throw new ArgumentNullException(nameof(consumerService));
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));

            _ = SearchAsync();
        }

        [RelayCommand]
        public async Task SearchAsync()
        {
            IsBusy = true;
            ErrorMessage = string.Empty;
            try
            {
                var results = await _consumerService.SearchFacultyStaffAsync(
                    string.IsNullOrWhiteSpace(SearchQuery) ? null : SearchQuery.Trim(),
                    SelectedFacultyType,
                    SelectedDepartment,
                    SelectedActiveStatus
                );

                FacultyStaffMembers.Clear();
                foreach (var member in results)
                {
                    FacultyStaffMembers.Add(member);
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed to load faculty/staff list: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task ClearFiltersAsync()
        {
            SearchQuery = string.Empty;
            SelectedFacultyType = null;
            SelectedDepartment = null;
            SelectedActiveStatus = null;

            await SearchAsync();
        }

        [RelayCommand]
        private void AddFaculty()
        {
            var vm = new FacultyStaffFormViewModel(_consumerService, null);
            var window = new Views.FacultyStaffFormWindow(vm);
            if (window.ShowDialog() == true)
            {
                _ = SearchAsync();
            }
        }

        [RelayCommand]
        private void EditFaculty(FacultyStaff? member)
        {
            if (member == null) return;

            var vm = new FacultyStaffFormViewModel(_consumerService, member);
            var window = new Views.FacultyStaffFormWindow(vm);
            if (window.ShowDialog() == true)
            {
                _ = SearchAsync();
            }
        }

        [RelayCommand]
        private async Task DeleteFacultyAsync(FacultyStaff? member)
        {
            if (member == null) return;

            var confirm = MessageBox.Show($"Are you sure you want to delete member '{member.Name}'? This will soft-delete their account but retain fine/borrowing histories.",
                "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (confirm == MessageBoxResult.Yes)
            {
                IsBusy = true;
                try
                {
                    var userId = _authService.CurrentUser?.Id ?? 1;
                    await _consumerService.DeleteFacultyStaffAsync(member.Id, "Deleted by librarian from UI", userId);
                    await SearchAsync();
                }
                catch (Exception ex)
                {
                    ErrorMessage = $"Failed to delete member: {ex.Message}";
                }
                finally
                {
                    IsBusy = false;
                }
            }
        }

        [RelayCommand]
        private void ViewProfile(FacultyStaff? member)
        {
            if (member == null) return;

            var vm = new ConsumerProfileViewModel(_consumerService, member.Id, MemberType.FacultyStaff);
            var window = new Views.ConsumerProfileWindow(vm);
            window.ShowDialog();
        }
    }
}
