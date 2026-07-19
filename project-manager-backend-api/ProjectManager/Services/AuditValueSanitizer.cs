using System.Collections;
using System.Reflection;
using System.Text.Json;

namespace ProjectManager.Services
{
    public static class AuditValueSanitizer
    {
        private static readonly string[] SensitiveNameFragments =
        {
            "password", "token", "authorization", "secret", "jwt", "apikey", "appkey", "credential"
        };

        public static string? Serialize(object? value)
        {
            if (value == null)
            {
                return null;
            }

            return JsonSerializer.Serialize(SanitizeValue(value, new HashSet<object>(ReferenceEqualityComparer.Instance)));
        }

        private static object? SanitizeValue(object? value, HashSet<object> visited)
        {
            if (value == null || value is string || value.GetType().IsValueType)
            {
                return value;
            }

            if (!visited.Add(value))
            {
                return "[Circular Reference]";
            }

            if (value is IDictionary dictionary)
            {
                var sanitizedDictionary = new Dictionary<string, object?>();
                foreach (DictionaryEntry entry in dictionary)
                {
                    var key = entry.Key?.ToString() ?? string.Empty;
                    sanitizedDictionary[key] = IsSensitive(key) ? "[REDACTED]" : SanitizeValue(entry.Value, visited);
                }
                return sanitizedDictionary;
            }

            if (value is IEnumerable enumerable)
            {
                return enumerable.Cast<object?>().Select(item => SanitizeValue(item, visited)).ToList();
            }

            var result = new Dictionary<string, object?>();
            foreach (var property in value.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public)
                         .Where(property => property.CanRead && property.GetIndexParameters().Length == 0))
            {
                object? propertyValue;
                try
                {
                    propertyValue = property.GetValue(value);
                }
                catch
                {
                    propertyValue = "[Unavailable]";
                }

                result[property.Name] = IsSensitive(property.Name)
                    ? "[REDACTED]"
                    : SanitizeValue(propertyValue, visited);
            }

            return result;
        }

        private static bool IsSensitive(string name)
        {
            return SensitiveNameFragments.Any(fragment =>
                name.Contains(fragment, StringComparison.OrdinalIgnoreCase));
        }
    }
}
