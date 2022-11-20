namespace Dax.Template.Extensions
{
    using System;

    internal static class StringExtensions
    {
        public static bool IsNullOrEmpty(this string? value)
        {
            return string.IsNullOrEmpty(value);
        }

        public static bool EqualsI(this string? current, string? value)
        {
            return current?.Equals(value, StringComparison.OrdinalIgnoreCase) ?? false;
        }

        public static string? GetDaxTableName(this string? name)
        {
            return name?.Replace("'", "''");
        }

        public static string? GetDaxColumnName(this string? name)
        {
            return name?.Replace("]", "]]");
        }

        /// <summary>
        /// Replace all occurrences of CRLF with LF since this is the default EOL character in SSAS
        /// </summary>
        public static string? ToASEol(this string? value)
        {
            if (value?.Length > 0)
            {
                value = value.Replace("\r\n", "\n");
            }

            return value;
        }
    }
}
