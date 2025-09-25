namespace gapir.Extensions;

public static class StringExtensions
{

    public static string SubstringBefore(this string str, char separator)
    {
        if (string.IsNullOrEmpty(str))
        {
            return str ?? "";
        }

        var index = str.IndexOf(separator);
        return index > 0 ? str[..index] : str;
    }
}