using System;
using CommunityToolkit.Mvvm.ComponentModel;
using KicsitLibrary.Core.Entities;
using KicsitLibrary.Core.Enums;

namespace KicsitLibrary.Desktop.ViewModels
{
    public partial class PendingReturnItemViewModel : ObservableObject
    {
        private readonly Action _onFineChanged;

        public IssueRecord Issue { get; }
        public string AccessionNumber => Issue.AccessionNumber;
        public string BookTitle => Issue.BookCopy.BookMaster.Title;
        public string BorrowerName => Issue.MemberType == MemberType.Student 
            ? (Issue.Student?.Name ?? "Student") 
            : (Issue.FacultyStaff?.Name ?? "Faculty/Staff");
        public string BorrowerTypeDisplay => Issue.MemberType.ToString();
        public string IssueDateDisplay => Issue.IssueDate.ToString("dd-MMM-yyyy");
        public string DueDateDisplay => Issue.ExpectedReturnDate.ToString("dd-MMM-yyyy");

        [ObservableProperty] private int _overdueDays;
        [ObservableProperty] private decimal _calculatedFine;
        [ObservableProperty] private string _selectedCondition = "Normal";

        public PendingReturnItemViewModel(IssueRecord issue, Action onFineChanged)
        {
            Issue = issue ?? throw new ArgumentNullException(nameof(issue));
            _onFineChanged = onFineChanged ?? throw new ArgumentNullException(nameof(onFineChanged));

            CalculateOverdueDays();
            UpdateCalculatedFine();
        }

        private void CalculateOverdueDays()
        {
            if (DateTime.UtcNow > Issue.ExpectedReturnDate)
            {
                OverdueDays = (int)(DateTime.UtcNow - Issue.ExpectedReturnDate).TotalDays;
            }
            else
            {
                OverdueDays = 0;
            }
        }

        partial void OnSelectedConditionChanged(string value)
        {
            UpdateCalculatedFine();
            _onFineChanged();
        }

        public void UpdateCalculatedFine()
        {
            decimal baseFine = OverdueDays * Issue.FinePerDay;

            if (SelectedCondition == "Lost" || SelectedCondition == "Damaged")
            {
                var bookPrice = Issue.BookCopy.BookMaster.PurchasePrice;
                CalculatedFine = baseFine + bookPrice + 200; // Book Price + Rs.200 surcharge
            }
            else
            {
                CalculatedFine = baseFine;
            }
        }
    }
}
