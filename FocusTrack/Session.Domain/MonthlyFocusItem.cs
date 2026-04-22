namespace Session.Domain
{
    public record MonthlyFocusItem(
      Guid UserId,
      int Year,
      int Month,
      decimal TotalDurationMin);
}
