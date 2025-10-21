namespace CRoutines.Coroutine.Extensions;

public static class TimeSpanExtensions
{
    extension(int val)
    {
        public TimeSpan Second => TimeSpan.FromSeconds(val);
        
        public TimeSpan Minute => TimeSpan.FromMinutes(val);
        
        public TimeSpan Hour => TimeSpan.FromHours(val);
        
        public TimeSpan Day => TimeSpan.FromDays(val);
        
        public TimeSpan Millis => TimeSpan.FromMilliseconds(val);
        
        public TimeSpan Micros => TimeSpan.FromMicroseconds(val);
    }
}