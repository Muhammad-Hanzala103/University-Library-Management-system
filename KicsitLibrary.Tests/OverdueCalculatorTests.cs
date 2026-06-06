using KicsitLibrary.Core.Helpers;

namespace KicsitLibrary.Tests;

public class OverdueCalculatorTests
{
    [Fact]
    public void CalculateFine_NeverReturnsNegativeAmount()
    {
        var fine = OverdueCalculator.CalculateFine(-2, 10);

        Assert.Equal(0, fine);
    }

    [Fact]
    public void CalculateOverdueDays_ReturnsZeroWhenNotOverdue()
    {
        var dueDate = new DateTime(2026, 6, 10, 12, 0, 0, DateTimeKind.Utc);

        var days = OverdueCalculator.CalculateOverdueDays(dueDate, dueDate.AddHours(-1));

        Assert.Equal(0, days);
    }

    [Fact]
    public void CalculateOverdueDays_ReturnsElapsedWholeDaysWhenPastDue()
    {
        var dueDate = new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);

        var days = OverdueCalculator.CalculateOverdueDays(dueDate, dueDate.AddDays(4).AddHours(3));

        Assert.Equal(4, days);
    }
}
