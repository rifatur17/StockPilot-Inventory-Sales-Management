using StockPilot.Core;

namespace StockPilot.UnitTests;

public sealed class DateTests
{
    [Fact]
    public void ReversedDatesAreRejected()
        => Assert.Throws<BusinessException>(() => UtcDates.Validate(new DateTime(2026, 2, 2), new DateTime(2026, 2, 1)));

    [Fact]
    public void ExclusiveUpperBoundCannotOverflow()
        => Assert.Throws<BusinessException>(() => UtcDates.Validate(null, DateTime.MaxValue));

    [Fact]
    public void InclusiveSameDayAndOpenDatesAreAllowed()
    {
        UtcDates.Validate(new DateTime(2026, 2, 2, 23, 59, 0), new DateTime(2026, 2, 2));
        UtcDates.Validate(null, null);
    }
}
