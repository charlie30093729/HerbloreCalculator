using System;

namespace HerbloreCalculator.Utils
{
    public static class TimeHelper
    {
        public static DateTime UnixTimeToLocal(long unixTime)
        {
            var dateTimeOffset = DateTimeOffset.FromUnixTimeSeconds(unixTime);
            return dateTimeOffset.LocalDateTime;
        }
    }
}
