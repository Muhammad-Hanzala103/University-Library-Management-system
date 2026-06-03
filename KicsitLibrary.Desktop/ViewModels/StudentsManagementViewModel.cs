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
    public partial class StudentsManagementViewModel : ObservableObject
    {
        private readonly IConsumerService _consumerService;
        private readonly IAuthenticationService _authService;

        [ObservableProperty]
        private ObservableCollection<Student> _students = new();

        // Filters lists
        public ObservableCollection<string> Programs { get; } = new() { "BSCS", "BSSE", "BSCE", "BSAI", "BSDS", "MCS", "BBA", "Other" };
        public ObservableCollection<string> Departments { get; } = new() { "CS", "CE", "SE", "AI", "DS", "General" };
        public ObservableCollection<string> Batches { get; } = new() { "2022", "2023", "2024", "2025", "2026" };
        public ObservableCollection<ClearanceStatus> ClearanceStatuses { get; } = new() { ClearanceStatus.NotCleared, ClearanceStatus.Cleared };

        // Search Filter Properties
        [ObservableProperty]
        private string _searchQuery = string.Empty;

        [ObservableProperty]
        private string? _selectedProgram;

        [ObservableProperty]
        private string? _selectedDepartment;

        [ObservableProperty]
        private string? _selectedBatch;

        [ObservableProperty]
        private ClearanceStatus? _selectedClearanceStatus;

        [ObservableProperty]
        private bool? _selectedActiveStatus;

        [ObservableProperty]
        private string _errorMessage = string.Empty;

        [ObservableProperty]
        private bool _isBusy;

        public StudentsManagementViewModel(IConsumerService consumerService, IAuthenticationService authService)
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
                var results = await _consumerService.SearchStudentsAsync(
                    string.IsNullOrWhiteSpace(SearchQuery) ? null : SearchQuery.Trim(),
                    SelectedProgram,
                    SelectedDepartment,
                    SelectedBatch,
                    SelectedClearanceStatus,
                    SelectedActiveStatus
                );

                Students.Clear();
                foreach (var student in results)
                {
                    Students.Add(student);
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed to load students list: {ex.Message}";
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
            SelectedProgram = null;
            SelectedDepartment = null;
            SelectedBatch = null;
            SelectedClearanceStatus = null;
            SelectedActiveStatus = null;

            await SearchAsync();
        }

        [RelayCommand]
        private void AddStudent()
        {
            var vm = new StudentFormViewModel(_consumerService, null);
            var window = new Views.StudentFormWindow(vm);
            if (window.ShowDialog() == true)
            {
                _ = SearchAsync();
            }
        }

        [RelayCommand]
        private void EditStudent(Student? student)
        {
            if (student == null) return;

            var vm = new StudentFormViewModel(_consumerService, student);
            var window = new Views.StudentFormWindow(vm);
            if (window.ShowDialog() == true)
            {
                _ = SearchAsync();
            }
        }

        [RelayCommand]
        private async Task DeleteStudentAsync(Student? student)
        {
            if (student == null) return;

            var confirm = MessageBox.Show($"Are you sure you want to delete student '{student.Name}'? This will soft-delete their account but retain fine/borrowing histories.",
                "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (confirm == MessageBoxResult.Yes)
            {
                IsBusy = true;
                try
                {
                    var userId = _authService.CurrentUser?.Id ?? 1;
                    await _consumerService.DeleteStudentAsync(student.Id, "Deleted by librarian from UI", userId);
                    await SearchAsync();
                }
                catch (Exception ex)
                {
                    ErrorMessage = $"Failed to delete student: {ex.Message}";
                }
                finally
                {
                    IsBusy = false;
                }
            }
        }

        [RelayCommand]
        private void ViewProfile(Student? student)
        {
            if (student == null) return;

            var vm = new ConsumerProfileViewModel(_consumerService, student.Id, MemberType.Student);
            var window = new Views.ConsumerProfileWindow(vm);
            window.ShowDialog();
        }
    }
}
