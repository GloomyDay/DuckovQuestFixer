using System;
using System.Reflection;

namespace QuestFixer.Logics
{
    /// <summary>
    /// Utility class for reflection operations - getting/setting values dynamically
    /// </summary>
    public static class ReflectionHelper
    {
        // Tries to get a value from an object by checking multiple possible property/field names
        // Searches both properties and fields with public/private bindings
        // Returns: The value of type T if found, or default(T) if not found
        public static T GetValue<T>(object obj, Type type, params string[] names)
        {
            foreach (var name in names)
            {
                try
                {
                    var prop = type.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (prop != null)
                    {
                        var value = prop.GetValue(obj);
                        if (value is T t) return t;
                        if (value != null && typeof(T) == typeof(string)) return (T)(object)value.ToString();
                    }

                    var field = type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (field != null)
                    {
                        var value = field.GetValue(obj);
                        if (value is T t) return t;
                        if (value != null && typeof(T) == typeof(string)) return (T)(object)value.ToString();
                    }
                }
                catch { }
            }
            return default;
        }
        
        // Sets a private field value on an object if the field exists and types match
        // Used for resetting quest task fields like "amount", "submitted", etc.
        // Returns: void
        public static void SetFieldIfExists(object obj, Type type, string fieldName, object value)
        {
            var field = type.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null && field.FieldType == value.GetType())
            {
                field.SetValue(obj, value);
            }
        }
    }
}
