using System;

namespace KicsitLibrary.Core.Helpers
{
    public static class OverdueCalculator
    {
        public static int CalculateOverdueDays(DateTime expectedReturnDate, DateTime asOf)
        {
            var dueDate = expectedReturnDate.Date;
            var currentDate = asOf.Date;

            if (currentDate <= dueDate)
            {
                return 0;
            }

            return Math.Max(0, (currentDate - dueDate).Days);
        }

        public static decimal CalculateFine(int overdueDays, decimal finePerDay)
        {
            return Math.Max(0, overdueDays) * Math.Max(0, finePerDay);
        }
    }
}
