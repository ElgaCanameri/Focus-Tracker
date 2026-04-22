using Microsoft.EntityFrameworkCore;

namespace RewardWorker.Services
{
    public class DailyGoalEvaluator
    {
        private const decimal DailyGoalMinutes = 120.00m;

        private readonly RewardDbContext _context;

        public DailyGoalEvaluator(RewardDbContext context) => _context = context;
        public async Task<(bool GoalReached, Guid? TriggeringSessionId)> EvaluateAsync(string userId, DateTime date, CancellationToken ct = default)
        {
            var startOfDay = date.Date;
            var endOfDay = startOfDay.AddDays(1);

            // check if badge already awarded today
            var alreadyAchieved = await _context.Sessions
                .AnyAsync(s =>
                    s.UserId == userId &&
                    s.StartTime >= startOfDay &&
                    s.StartTime < endOfDay &&
                    s.IsDailyGoalAchieved, ct);

            if (alreadyAchieved)
                return (false, null);

            // get all sessions for today
            var todaySessions = await _context.Sessions
                .Where(s =>
                    s.UserId == userId &&
                    s.StartTime >= startOfDay &&
                    s.StartTime < endOfDay)
                .OrderBy(s => s.StartTime)
                .ToListAsync(ct);

            // calculate running total
            decimal runningTotal = 0;
            foreach (var session in todaySessions)
            {
                runningTotal += session.DurationMin.Value;

                if (runningTotal >= DailyGoalMinutes)
                    return (true, session.Id); // ← this session triggered the goal
            }

            return (false, null);
        }
    }
}
