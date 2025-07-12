namespace Kurz.Utilities;

/// <summary>
/// Utility class for Base62 encoding and decoding.
/// Base62 uses digits 0-9, lowercase a-z, and uppercase A-Z.
/// </summary>
public static class Base62
{
    private const string Base62Chars = "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";
    private const int Base = 62;

    /// <summary>
    /// Encodes a non-negative long integer to Base62 string.
    /// </summary>
    /// <param name="value">The value to encode</param>
    /// <returns>Base62 encoded string</returns>
    public static string Encode(long value)
    {
        if (value < 0)
            throw new ArgumentException("Value must be non-negative", nameof(value));

        if (value == 0)
            return "0";

        var result = new System.Text.StringBuilder();
        while (value > 0)
        {
            result.Insert(0, Base62Chars[(int)(value % Base)]);
            value /= Base;
        }

        return result.ToString();
    }

    /// <summary>
    /// Decodes a Base62 string to a long integer.
    /// </summary>
    /// <param name="base62String">The Base62 string to decode</param>
    /// <returns>Decoded long integer</returns>
    /// <exception cref="ArgumentException">Thrown when the string contains invalid Base62 characters</exception>
    public static long Decode(string base62String)
    {
        if (string.IsNullOrEmpty(base62String))
            throw new ArgumentException("Base62 string cannot be null or empty", nameof(base62String));

        long result = 0;
        long multiplier = 1;

        for (int i = base62String.Length - 1; i >= 0; i--)
        {
            char c = base62String[i];
            int charValue = Base62Chars.IndexOf(c);
            
            if (charValue == -1)
                throw new ArgumentException($"Invalid Base62 character: {c}", nameof(base62String));

            result += charValue * multiplier;
            multiplier *= Base;
        }

        return result;
    }

    /// <summary>
    /// Determines if a string is a valid Base62 encoded string.
    /// </summary>
    /// <param name="value">The string to check</param>
    /// <returns>True if the string is valid Base62, false otherwise</returns>
    public static bool IsValidBase62(string value)
    {
        if (string.IsNullOrEmpty(value))
            return false;

        return value.All(c => Base62Chars.Contains(c));
    }

    /// <summary>
    /// Determines if a string represents a decimal number.
    /// </summary>
    /// <param name="value">The string to check</param>
    /// <returns>True if the string is a valid decimal number, false otherwise</returns>
    public static bool IsDecimal(string value)
    {
        return !string.IsNullOrEmpty(value) && value.All(char.IsDigit);
    }
}
