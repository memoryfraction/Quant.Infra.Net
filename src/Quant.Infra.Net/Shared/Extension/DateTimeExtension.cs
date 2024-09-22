using System;

namespace Quant.Infra.Net.Shared.Extension
{
    public static class DateTimeExtension
    {
        /// <summary>
        ///  Calculates the start date of the week for the given date.
        /// </summary>
        /// <param name="dt"></param>
        /// <param name="startOfWeek"></param>
        /// <returns></returns>
        public static DateTime StartOfWeek(this DateTime dt, DayOfWeek startOfWeek)
        {
            int diff = (7 + (dt.DayOfWeek - startOfWeek)) % 7;
            return dt.AddDays(-1 * diff).Date;
        }
    }
}