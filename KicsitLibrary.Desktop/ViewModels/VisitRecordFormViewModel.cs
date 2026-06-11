using System;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KicsitLibrary.Core.Entities;
using KicsitLibrary.Core.Interfaces;

namespace KicsitLibrary.Desktop.ViewModels
{
    public partial class VisitRecordFormViewModel : ObservableObject
    {
        private readonly IConsumerService _consumerService;
        private readonly IAuthenticationService _authService;
        private readonly VisitRecord? _editingRecord;

        [ObservableProperty]
        private string _windowTitle = "Add Visit Record";

        // Form Fields
        [ObservableProperty]
        private string _visitNumber = string.Empty;

        [ObservableProperty]
        private string _organizationName = string.Empty;

        [ObservableProperty]
        private string _visitType = "ExternalVisit";

        [ObservableProperty]
        private DateTime _visitDate = DateTime.Now;

        [ObservableProperty]
        private string _visitTeamMembers = string.Empty;

        [ObservableProperty]
        private string _department = "General";

        [ObservableProperty]
        private string _purpose = string.Empty;

        [ObservableProperty]
        private string _observations = string.Empty;

        [ObservableProperty]
        private string _findings = string.Empty;

        [ObservableProperty]
        private string _suggestions = string.Empty;

        [ObservableProperty]
        private string _requirements = string.Empty;

        [ObservableProperty]
        private string _actionTaken = string.Empty;

        [ObservableProperty]
        private DateTime? _nextFollowUpDate;

        [ObservableProperty]
        private string _contact = string.Empty;

        [ObservableProperty]
        private string _remarks = string.Empty;

        [ObservableProperty]
        private string _errorMessage = string.Empty;

        [ObservableProperty]
        private bool _isBusy;

        public VisitRecordFormViewModel(IConsumerService consumerService, IAuthenticationService authService, VisitRecord? editingRecord)
        {
            _consumerService = consumerService ?? throw new ArgumentNullException(nameof(consumerService));
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));
            _editingRecord = editingRecord;

            if (_editingRecord != null)
            {
                WindowTitle = $"Edit Visit Record: {_editingRecord.VisitNumber}";
                VisitNumber = _editingRecord.VisitNumber;
                OrganizationName = _editingRecord.OrganizationName;
                VisitType = _editingRecord.VisitType;
                VisitDate = _editingRecord.VisitDate;
                VisitTeamMembers = _editingRecord.VisitTeamMembers;
                Department = _editingRecord.Department;
                Purpose = _editingRecord.Purpose;
                Observations = _editingRecord.Observations;
                Findings = _editingRecord.Findings;
                Suggestions = _editingRecord.Suggestions;
                Requirements = _editingRecord.Requirements;
                ActionTaken = _editingRecord.ActionTaken;
                NextFollowUpDate = _editingRecord.NextFollowUpDate;
                Contact = _editingRecord.Contact ?? string.Empty;
                Remarks = _editingRecord.Remarks ?? string.Empty;
            }
        }

        [RelayCommand]
        private async Task SaveAsync(Window? window)
        {
            if (string.IsNullOrWhiteSpace(OrganizationName))
            {
                ErrorMessage = "Organization Name is required.";
                return;
            }
            if (string.IsNullOrWhiteSpace(Purpose))
            {
                ErrorMessage = "Purpose of visit is required.";
                return;
            }

            IsBusy = true;
            ErrorMessage = string.Empty;

            try
            {
                var record = _editingRecord ?? new VisitRecord();
                record.OrganizationName = OrganizationName.Trim();
                record.VisitType = VisitType.Trim();
                record.VisitDate = VisitDate;
                record.VisitTeamMembers = VisitTeamMembers.Trim();
                record.Department = Department.Trim();
                record.Purpose = Purpose.Trim();
                record.Observations = Observations.Trim();
                record.Findings = Findings.Trim();
                record.Suggestions = Suggestions.Trim();
                record.Requirements = Requirements.Trim();
                record.ActionTaken = ActionTaken.Trim();
                record.NextFollowUpDate = NextFollowUpDate;
                record.Contact = string.IsNullOrWhiteSpace(Contact) ? null : Contact.Trim();
                record.Remarks = string.IsNullOrWhiteSpace(Remarks) ? null : Remarks.Trim();

                if (_editingRecord == null)
                {
                    record.CreatedByUserId = _authService.CurrentUser?.Id ?? 1;
                    await _consumerService.AddVisitRecordAsync(record);
                }
                else
                {
                    record.UpdatedByUserId = _authService.CurrentUser?.Id ?? 1;
                    await _consumerService.UpdateVisitRecordAsync(record);
                }

                if (window != null)
                {
                    window.DialogResult = true;
                    window.Close();
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed to save visit record: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private void Cancel(Window? window)
        {
            if (window != null)
            {
                window.DialogResult = false;
                window.Close();
            }
        }
    }
}
