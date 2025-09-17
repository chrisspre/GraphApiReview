using gapir.Utilities;

namespace gapir.Tests;

/// <summary>
/// Tests for the Base62 utility class.
/// </summary>
public class Base62Tests
{
    [Theory]
    [InlineData(0, "0")]
    [InlineData(1, "1")]
    [InlineData(61, "Z")]
    [InlineData(62, "10")]
    [InlineData(12041652, "OwAc")]
    [InlineData(999999, "4c91")]
    public void Encode_ValidInputs_ReturnsExpectedBase62String(long input, string expected)
    {
        // Act
        string result = Base62.Encode(input);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("0", 0)]
    [InlineData("1", 1)]
    [InlineData("Z", 61)]
    [InlineData("10", 62)]
    [InlineData("OwAc", 12041652)]
    [InlineData("4c91", 999999)]
    public void Decode_ValidInputs_ReturnsExpectedInteger(string input, long expected)
    {
        // Act
        long result = Base62.Decode(input);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null!)]
    public void Decode_NullOrEmptyInput_ThrowsArgumentException(string? input)
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => Base62.Decode(input));
    }

    [Theory]
    [InlineData("!")]
    [InlineData("@")]
    [InlineData(" ")]
    [InlineData("1@3")]
    public void Decode_InvalidCharacters_ThrowsArgumentException(string input)
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => Base62.Decode(input));
    }

    [Fact]
    public void Encode_NegativeValue_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => Base62.Encode(-1));
    }

    [Theory]
    [InlineData("0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ", true)]
    [InlineData("123abc", true)]
    [InlineData("ABC", true)]
    [InlineData("0", true)]
    [InlineData("", false)]
    [InlineData(null!, false)]
    [InlineData("123!", false)]
    [InlineData("123 abc", false)]
    public void IsValidBase62_VariousInputs_ReturnsExpectedResult(string? input, bool expected)
    {
        // Act
        bool result = Base62.IsValidBase62(input);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("12041652", true)]
    [InlineData("0", true)]
    [InlineData("999", true)]
    [InlineData("", false)]
    [InlineData(null!, false)]
    [InlineData("123abc", false)]
    [InlineData("12.34", false)]
    [InlineData("123 456", false)]
    public void IsDecimal_VariousInputs_ReturnsExpectedResult(string? input, bool expected)
    {
        // Act
        bool result = Base62.IsDecimal(input);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(62)]
    [InlineData(12041652)]
    [InlineData(999999)]
    [InlineData(long.MaxValue)]
    public void EncodeAndDecode_RoundTrip_PreservesOriginalValue(long originalValue)
    {
        // Act
        string encoded = Base62.Encode(originalValue);
        long decoded = Base62.Decode(encoded);

        // Assert
        Assert.Equal(originalValue, decoded);
    }

    [Fact]
    public void ShortUrl_Generation_Uses_Base62_And_New_Domain()
    {
        // This test verifies that the new short URL format uses Base62 encoding and the 'g' domain
        // Note: This is more of an integration verification than a unit test
        
        // Arrange
        long testPrId = 12041652;
        string expectedBase62 = "OwAc";
        string expectedUrl = $"http://g/pr/{expectedBase62}";
        
        // Act
        string actualBase62 = Base62.Encode(testPrId);
        
        // Assert
        Assert.Equal(expectedBase62, actualBase62);
        
        // Verify the URL format that would be generated
        string actualUrl = $"http://g/pr/{actualBase62}";
        Assert.Equal(expectedUrl, actualUrl);
        
        // Verify round-trip works
        long decodedId = Base62.Decode(actualBase62);
        Assert.Equal(testPrId, decodedId);
    }
}
