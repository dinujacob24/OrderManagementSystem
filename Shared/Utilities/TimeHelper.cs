using System;

namespace Shared.Utilities
{
    public static class TimeHelper
    {
        public static DateTime GetIstNow()
        {
            // India Standard Time (UTC+5:30)
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("India Standard Time"));
        }
    }
}
