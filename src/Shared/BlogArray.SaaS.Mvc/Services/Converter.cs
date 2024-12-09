using System.Globalization;

namespace BlogArray.SaaS.Mvc.Services;

public class Converter
{
    public static int ToInt(object value, int defaultValue = 0)
    {
        value = CheckNulls(value);

        return int.TryParse(value.ToString().Replace(",", ""), out int retValue) ? retValue : defaultValue;
    }

    public static int? ToInt(object value, int? defaultValue)
    {
        value = CheckNulls(value);

        return int.TryParse(value.ToString().Replace(",", ""), out int retValue) ? retValue : defaultValue;
    }

    public static double ToDouble(object value, double defaultValue = 0)
    {
        value = CheckNulls(value);

        return double.TryParse(value.ToString(), out double retValue) ? retValue : defaultValue;
    }

    public static decimal ToDecimal(object value, decimal defaultValue = 0)
    {
        value = CheckNulls(value);

        return decimal.TryParse(value.ToString(), out decimal retValue) ? retValue : defaultValue;
    }

    public static float ToFloat(object value, float defaultValue = 0)
    {
        value = CheckNulls(value);

        return float.TryParse(value.ToString(), out float retValue) ? retValue : defaultValue;
    }

    public static bool ToBoolean(object value, bool defaultValue = true)
    {
        value = CheckNulls(value);

        return bool.TryParse(value.ToString(), out bool retValue) ? retValue : defaultValue;
    }

    public static string ToString(object value)
    {
        try
        {
            return value?.ToString() ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    public static DateTime ToDateTime(object value)
    {
        value = CheckNulls(value);

        return DateTime.TryParse(value.ToString(), out DateTime retValue) ? retValue : DateTime.Now;
    }

    public static DateTime ToDateTime(string value, DateTimeStyles style)
    {
        return DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, style, out DateTimeOffset retValue)
            ? retValue.UtcDateTime
            : DateTime.Now;
    }

    public static long ToLong(object value, long defaultValue = 0)
    {
        value = CheckNulls(value);

        return long.TryParse(value.ToString(), out long retValue) ? retValue : defaultValue;
    }

    public static short ToShort(object value, short defaultValue = 0)
    {
        value = CheckNulls(value);

        return short.TryParse(value.ToString(), out short retValue) ? retValue : defaultValue;
    }

    public static string CheckNulls(object value, string defaultValue = "")
    {
        return value != null ? value.ToString() : defaultValue;
    }

    public static DateTime ToDateTime(string value, string format)
    {
        value = CheckNulls(value);

        return DateTime.TryParseExact(value, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime retValue)
            ? retValue
            : DateTime.Now;
    }

}
