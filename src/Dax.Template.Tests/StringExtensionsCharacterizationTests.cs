namespace Dax.Template.Tests
{
    using Dax.Template.Extensions;
    using Xunit;

    /// <summary>
    /// Characterization tests for the small string-transformation helpers in
    /// <see cref="Dax.Template.Extensions.StringExtensions"/>: DAX name escaping/quoting
    /// (<see cref="StringExtensions.GetDaxTableName"/>, <see cref="StringExtensions.GetDaxColumnName"/>),
    /// EOL normalization (<see cref="StringExtensions.ToASEol"/>), and the small null/case helpers
    /// (<see cref="StringExtensions.IsNullOrEmpty"/>, <see cref="StringExtensions.EqualsI"/>). These are
    /// `internal` and reachable here via [InternalsVisibleTo("Dax.Template.Tests")] declared in
    /// src/Dax.Template/AssemblyInfo.cs. Each test pins the CURRENT transformation, including edge cases
    /// (null/empty input, already-escaped input, embedded delimiters).
    /// </summary>
    public class StringExtensionsCharacterizationTests
    {
        [Theory]
        [InlineData(null, true)]
        [InlineData("", true)]
        [InlineData(" ", false)]
        [InlineData("value", false)]
        public void IsNullOrEmpty_VariousInputs_MatchesStringIsNullOrEmpty(string? value, bool expected)
        {
            // Act
            var actual = value.IsNullOrEmpty();

            // Assert
            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData("ABC", "abc", true)]
        [InlineData("ABC", "ABC", true)]
        [InlineData("ABC", "DEF", false)]
        [InlineData("", "", true)]
        public void EqualsI_NonNullCurrent_IsCaseInsensitiveOrdinalComparison(string current, string value, bool expected)
        {
            // Act
            var actual = current.EqualsI(value);

            // Assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void EqualsI_NullCurrent_ReturnsFalseEvenWhenValueIsAlsoNull()
        {
            // Arrange: current characterization -- the null-conditional short-circuits before the
            // Equals(null) comparison ever runs, so two "null" strings are NOT treated as equal here
            // (unlike string.Equals(null, null) semantics one might expect).
            string? current = null;

            // Act
            var actual = current.EqualsI(null);

            // Assert
            Assert.False(actual);
        }

        [Theory]
        [InlineData("Table", "Table")]
        [InlineData("It's a Table", "It''s a Table")]
        [InlineData("''already''", "''''already''''")]
        [InlineData("", "")]
        public void GetDaxTableName_EscapesSingleQuotes(string name, string expected)
        {
            // Act
            var actual = name.GetDaxTableName();

            // Assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void GetDaxTableName_NullInput_ReturnsNull()
        {
            // Arrange
            string? name = null;

            // Act
            var actual = name.GetDaxTableName();

            // Assert
            Assert.Null(actual);
        }

        [Theory]
        [InlineData("Column", "Column")]
        [InlineData("Value]", "Value]]")]
        [InlineData("]]already]]", "]]]]already]]]]")]
        [InlineData("", "")]
        public void GetDaxColumnName_EscapesClosingBrackets(string name, string expected)
        {
            // Act
            var actual = name.GetDaxColumnName();

            // Assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void GetDaxColumnName_NullInput_ReturnsNull()
        {
            // Arrange
            string? name = null;

            // Act
            var actual = name.GetDaxColumnName();

            // Assert
            Assert.Null(actual);
        }

        [Theory]
        [InlineData("line1\r\nline2", "line1\nline2")]
        [InlineData("a\r\nb\r\nc", "a\nb\nc")]
        [InlineData("no-eol-here", "no-eol-here")]
        public void ToASEol_ReplacesCrLfWithLf(string value, string expected)
        {
            // Act
            var actual = value.ToASEol();

            // Assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void ToASEol_LoneCarriageReturnNotFollowedByLineFeed_IsLeftUnchanged()
        {
            // Arrange: current characterization -- only the exact "\r\n" pair is normalized; a bare "\r"
            // (e.g. old Mac-style line endings, or a stray \r not part of a CRLF pair) survives untouched.
            string value = "line1\rline2";

            // Act
            var actual = value.ToASEol();

            // Assert
            Assert.Equal("line1\rline2", actual);
        }

        [Fact]
        public void ToASEol_EmptyString_ReturnsEmptyStringUnchanged()
        {
            // Arrange: the `value?.Length > 0` guard means the replace branch is skipped for an empty
            // string, but the (unchanged) value is still returned rather than null.
            string value = "";

            // Act
            var actual = value.ToASEol();

            // Assert
            Assert.Equal("", actual);
        }

        [Fact]
        public void ToASEol_NullInput_ReturnsNull()
        {
            // Arrange
            string? value = null;

            // Act
            var actual = value.ToASEol();

            // Assert
            Assert.Null(actual);
        }
    }
}