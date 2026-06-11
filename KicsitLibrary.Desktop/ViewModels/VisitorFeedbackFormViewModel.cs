using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KicsitLibrary.Core.Entities;
using KicsitLibrary.Core.Interfaces;

namespace KicsitLibrary.Desktop.ViewModels
{
    public partial class VisitorFeedbackFormViewModel : ObservableObject
    {
        private readonly IConsumerService _consumerService;
        private readonly IAuthenticationService _authService;
        private readonly VisitorFeedback? _editingFeedback;

        [ObservableProperty] private string _windowTitle = "Add Visitor Feedback";
        [ObservableProperty] private string _visitorName = string.Empty;
        [ObservableProperty] private string _cnic = string.Empty;
        [ObservableProperty] private string _phone = string.Empty;
        [ObservableProperty] private string _email = string.Empty;
        [ObservableProperty] private string _visitPurpose = string.Empty;
        [ObservableProperty] private string _feedbackType = "Suggestion"; 
        [ObservableProperty] private string _feedbackText = string.Empty;
        [ObservableProperty] private string _status = "New"; 
        [ObservableProperty] private string _reviewedRemarks = string.Empty;
        [ObservableProperty] private bool _isEditingMode;
        [ObservableProperty] private string _errorMessage = string.Empty;
        [ObservableProperty] private bool _isBusy;

        public IReadOnlyList<string> FeedbackTypes { get; } = new List<string> { "Suggestion", "Complaint", "Appreciation", "Inquiry", "Other" };
        public IReadOnlyList<string> StatusOptions { get; } = new List<string> { "New", "Reviewed", "Closed" };

        public VisitorFeedbackFormViewModel(IConsumerService consumerService, IAuthenticationService authService, VisitorFeedback? editingFeedback)
        {
            _consumerService = consumerService;
            _authService = authService;
            _editingFeedback = editingFeedback;

            if (_editingFeedback != null)
            {
                IsEditingMode = true;
                WindowTitle = "Review/Edit Visitor Feedback";
                VisitorName = _editingFeedback.VisitorName;
                Cnic = _editingFeedback.CNIC ?? string.Empty;
                Phone = _editingFeedback.Phone ?? string.Empty;
                Email = _editingFeedback.Email ?? string.Empty;
                VisitPurpose = _editingFeedback.VisitPurpose;
                FeedbackType = _editingFeedback.FeedbackType;
                FeedbackText = _editingFeedback.FeedbackText;
                Status = _editingFeedback.Status;
                ReviewedRemarks = _editingFeedback.ReviewedRemarks ?? string.Empty;
            }
        }

        [RelayCommand]
        private async Task SaveAsync(Window? window)
        {
            if (string.IsNullOrWhiteSpace(VisitorName))
            {
                ErrorMessage = "Visitor Name is required.";
                return;
            }
            if (string.IsNullOrWhiteSpace(VisitPurpose))
            {
                ErrorMessage = "Visit Purpose is required.";
                return;
            }
            if (string.IsNullOrWhiteSpace(FeedbackText))
            {
                ErrorMessage = "Feedback text is required.";
                return;
            }

            IsBusy = true;
            ErrorMessage = string.Empty;

            try
            {
                var feedback = _editingFeedback ?? new VisitorFeedback();
                feedback.VisitorName = VisitorName.Trim();
                feedback.CNIC = string.IsNullOrWhiteSpace(Cnic) ? null : Cnic.Trim();
                feedback.Phone = string.IsNullOrWhiteSpace(Phone) ? null : Phone.Trim();
                feedback.Email = string.IsNullOrWhiteSpace(Email) ? null : Email.Trim();
                feedback.VisitPurpose = VisitPurpose.Trim();
                feedback.FeedbackType = FeedbackType;
                feedback.FeedbackText = FeedbackText.Trim();
                feedback.Status = Status;
                feedback.ReviewedRemarks = string.IsNullOrWhiteSpace(ReviewedRemarks) ? null : ReviewedRemarks.Trim();

                if (_editingFeedback == null)
                {
                    await _consumerService.AddVisitorFeedbackAsync(feedback);
                }
                else
                {
                    feedback.ReviewedByUserId = _authService.CurrentUser?.Id;
                    await _consumerService.UpdateVisitorFeedbackAsync(feedback);
                }

                if (window != null)
                {
                    window.DialogResult = true;
                    window.Close();
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed to save feedback: {ex.Message}";
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
