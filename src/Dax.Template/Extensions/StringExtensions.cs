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
    }
}
