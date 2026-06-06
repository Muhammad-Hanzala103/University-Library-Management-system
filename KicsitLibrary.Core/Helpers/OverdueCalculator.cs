using System;

namespace KicsitLibrary.Core.Helpers
{
    public static class OverdueCalculator
    {
        public static int CalculateOverdueDays(DateTime expectedReturnDate, DateTime asOf)
        {
            if (asOf <= expectedReturnDate)
            {
                return 0;
            }

            return Math.Max(0, (int)(asOf - expectedReturnDate).TotalDays);
        }

        public static decimal CalculateFine(int overdueDays, decimal finePerDay)
        {
            return Math.Max(0, overdueDays) * Math.Max(0, finePerDay);
        }
    }
}
