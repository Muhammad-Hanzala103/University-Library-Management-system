using System;
using System.Threading.Tasks;
using KicsitLibrary.Core.Entities;
using KicsitLibrary.Core.Interfaces;

namespace KicsitLibrary.Services.Logging
{
    public class ActivityLogService : IActivityLogService
    {
        private readonly IRepository<ActivityLog> _activityLogRepository;

        public ActivityLogService(IRepository<ActivityLog> activityLogRepository)
        {
            _activityLogRepository = activityLogRepository ?? throw new ArgumentNullException(nameof(activityLogRepository));
        }

        public async Task LogActivityAsync(string action, string detail, int? userId = null, string? ipAddress = null)
        {
            var log = new ActivityLog
            {
                Action = action,
                Detail = detail,
                UserId = userId,
                IpAddress = ipAddress ?? "127.0.0.1",
                CreatedAt = DateTime.UtcNow
            };

            await _activityLogRepository.AddAsync(log);
            await _activityLogRepository.SaveChangesAsync();
        }
    }
}
