using System;

namespace KicsitLibrary.Desktop.Helpers
{
    public static class FinancialYearState
    {
        public static bool IsCurrentYear
        {
            get => KicsitLibrary.Core.Helpers.FinancialYearState.IsCurrentYear;
            set => KicsitLibrary.Core.Helpers.FinancialYearState.IsCurrentYear = value;
        }

        public static string SelectedYear
        {
            get => KicsitLibrary.Core.Helpers.FinancialYearState.SelectedYear;
            set => KicsitLibrary.Core.Helpers.FinancialYearState.SelectedYear = value;
        }

        public static string GetCurrentFinancialYear() => KicsitLibrary.Core.Helpers.FinancialYearState.GetCurrentFinancialYear();
    }
}
