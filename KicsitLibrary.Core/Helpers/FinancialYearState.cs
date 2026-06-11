using System;

namespace KicsitLibrary.Core.Helpers
{
    public static class FinancialYearState
    {
        private static bool _isCurrentYear = true;
        public static bool IsCurrentYear
        {
            get => _isCurrentYear;
            set => _isCurrentYear = value;
        }
        
        private static string _customYear = "2025-2026";
        public static string SelectedYear 
        { 
            get => IsCurrentYear ? GetCurrentFinancialYear() : _customYear;
            set => _customYear = value;
        }

        public static string GetCurrentFinancialYear()
        {
            var today = DateTime.Today;
            int startYear = today.Month >= 7 ? today.Year : today.Year - 1;
            return $"{startYear}-{startYear + 1}";
        }
    }
}
