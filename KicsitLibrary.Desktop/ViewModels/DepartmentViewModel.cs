using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KicsitLibrary.Core.Entities;
using KicsitLibrary.Core.Interfaces;

namespace KicsitLibrary.Desktop.ViewModels
{
    public partial class DepartmentViewModel : ObservableObject
    {
        private readonly ICatalogService _catalogService;
        private readonly IAuthenticationService _authService;

        [ObservableProperty]
        private ObservableCollection<DepartmentCategory> _departments = new();

        private readonly List<DepartmentCategory> _allDepartments = new();

        [ObservableProperty]
        private string _searchText = string.Empty;

        [ObservableProperty]
        private DepartmentCategory? _selectedDepartment;

        [ObservableProperty]
        private string _name = string.Empty;

        [ObservableProperty]
        private string _description = string.Empty;

        [ObservableProperty]
        private string _errorMessage = string.Empty;

        [ObservableProperty]
        private bool _isBusy;

        [ObservableProperty]
        private bool _isEditMode;

        public DepartmentViewModel(ICatalogService catalogService, IAuthenticationService authService)
        {
            _catalogService = catalogService ?? throw new ArgumentNullException(nameof(catalogService));
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));
            _ = LoadDepartmentsAsync();
        }

        public async Task LoadDepartmentsAsync()
        {
            IsBusy = true;
            try
            {
                var deptsList = await _catalogService.GetAllDepartmentCategoriesAsync();
                _allDepartments.Clear();
                _allDepartments.AddRange(deptsList);
                ApplyFilter();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed to load departments: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        partial void OnSearchTextChanged(string value)
        {
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            Departments.Clear();
            var query = SearchText?.Trim() ?? string.Empty;
            foreach (var dept in _allDepartments)
            {
                if (string.IsNullOrEmpty(query) ||
                    dept.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    (dept.Description != null && dept.Description.Contains(query, StringComparison.OrdinalIgnoreCase)))
                {
                    Departments.Add(dept);
                }
            }
        }

        [RelayCommand]
        private void SelectDepartmentForEdit(DepartmentCategory? dept)
        {
            if (dept == null) return;

            SelectedDepartment = dept;
            Name = dept.Name;
            Description = dept.Description ?? string.Empty;
            IsEditMode = true;
            ErrorMessage = string.Empty;
        }

        [RelayCommand]
        private void ClearForm()
        {
            SelectedDepartment = null;
            Name = string.Empty;
            Description = string.Empty;
            IsEditMode = false;
            ErrorMessage = string.Empty;
        }

        [RelayCommand]
        private async Task SaveAsync()
        {
            if (string.IsNullOrWhiteSpace(Name))
            {
                ErrorMessage = "Department name is required.";
                return;
            }

            IsBusy = true;
            ErrorMessage = string.Empty;

            try
            {
                if (IsEditMode && SelectedDepartment != null)
                {
                    SelectedDepartment.Name = Name.Trim();
                    SelectedDepartment.Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim();

                    await _catalogService.UpdateDepartmentCategoryAsync(SelectedDepartment);
                }
                else
                {
                    var newDept = new DepartmentCategory
                    {
                        Name = Name.Trim(),
                        Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim()
                    };

                    await _catalogService.AddDepartmentCategoryAsync(newDept);
                }

                ClearForm();
                await LoadDepartmentsAsync();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed to save department: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task DeleteAsync(DepartmentCategory? dept)
        {
            if (dept == null) return;

            var currentUserId = _authService.CurrentUser?.Id ?? 1;

            IsBusy = true;
            ErrorMessage = string.Empty;

            try
            {
                await _catalogService.DeleteDepartmentCategoryAsync(dept.Id, "Deleted via admin portal", currentUserId);
                ClearForm();
                await LoadDepartmentsAsync();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed to delete department: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}
