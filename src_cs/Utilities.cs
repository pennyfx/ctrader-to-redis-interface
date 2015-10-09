using System;
using Newtonsoft.Json.Linq;

namespace CarbonFx.FOS
{
    public static partial class Utilities
    {
        public static long ToUnixTime(this DateTime t)
        {
            var timeSpan = t.Subtract(new DateTime(1970, 1, 1, 0, 0, 0));
            return (long)timeSpan.TotalSeconds;
        }

        public static bool ContainsKey(this JObject o, string key)
        {
            try
            {
                return o[key] != null;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
