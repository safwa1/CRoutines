using System.Numerics;

namespace CRoutines.Extensions;

public static class TimeSpanExtensions
{
#if NET10_0_OR_GREATER
    extension<T>(T val) where T : INumber<T>
    {
        public TimeSpan Second => TimeSpan.FromSeconds(double.CreateChecked(val));
        
        public TimeSpan Minute => TimeSpan.FromMinutes(double.CreateChecked(val));
        
        public TimeSpan Hour => TimeSpan.FromHours(double.CreateChecked(val));
        
        public TimeSpan Day => TimeSpan.FromDays(double.CreateChecked(val));
        
        public TimeSpan Millis => TimeSpan.FromMilliseconds(double.CreateChecked(val));
        
        public TimeSpan Micros => TimeSpan.FromMicroseconds(double.CreateChecked(val));
    }
    #else 
    public static TimeSpan Second<T>(this T val)
        where T : INumber<T>
        => TimeSpan.FromSeconds(double.CreateChecked(val));
        
    public static TimeSpan Minute<T>(this T val)
        where T : INumber<T>
        => TimeSpan.FromMinutes(double.CreateChecked(val));
        
    public static TimeSpan Hour<T>(this T val)
        where T : INumber<T>
        => TimeSpan.FromHours(double.CreateChecked(val));
        
    public static TimeSpan Day<T>(this T val)
        where T : INumber<T>
        => TimeSpan.FromDays(double.CreateChecked(val));
        
    public static TimeSpan Millis<T>(this T val)
        where T : INumber<T>
        => TimeSpan.FromMilliseconds(double.CreateChecked(val));
        
    public static TimeSpan Micros<T>(this T val)
        where T : INumber<T>
        => TimeSpan.FromMicroseconds(double.CreateChecked(val));
#endif
}