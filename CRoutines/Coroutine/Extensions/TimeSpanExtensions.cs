namespace CRoutines.Coroutine.Extensions;

public static class TimeSpanExtensions
{
    extension(int val)
    {
        public TimeSpan Seconds => TimeSpan.FromSeconds(val);
        
        public TimeSpan Minutes => TimeSpan.FromMinutes(val);
        
        public TimeSpan Hours => TimeSpan.FromHours(val);
        
        public TimeSpan Days => TimeSpan.FromDays(val);
        
        public TimeSpan Millis => TimeSpan.FromMilliseconds(val);
        
        public TimeSpan Micros => TimeSpan.FromMicroseconds(val);
    }
}