namespace StockPilot.Core;

public static class UtcDates
{
    public static void Validate(DateTime? from, DateTime? through)
    {
        if (through?.Date == DateTime.MaxValue.Date)
            throw new BusinessException("Through date must be before 9999-12-31.");
        if (from.HasValue && through.HasValue && from.Value.Date > through.Value.Date)
            throw new BusinessException("From date cannot be after through date.");
    }
}
