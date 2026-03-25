namespace SharePoint.Api.Helper
{
    public class StringHelper
    {
        public static Guid? NormalizeOptionalGuidOrRoot(string? value)
        {
            if (string.IsNullOrWhiteSpace(value) || IsRoot(value))
            {
                return Guid.Empty;
            }

            if (Guid.TryParse(value, out var parsed))
            {
                return parsed;
            }

            throw new ArgumentException("Invalid id format.", nameof(value));
        }

        public static Guid ParseRequiredGuid(string value, string paramName)
        {
            if (!Guid.TryParse(value, out var parsed))
            {
                throw new ArgumentException("Invalid id format.", paramName);
            }

            return parsed;
        }

        public static bool IsRoot(string value)
        {
            return string.Equals(value, "root", StringComparison.OrdinalIgnoreCase)
                || (Guid.TryParse(value, out var parsed) && parsed == Guid.Empty);
        }
    }
}
