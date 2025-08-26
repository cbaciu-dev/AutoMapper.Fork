namespace BKS.CustomMapper;

/// <summary>
/// Centralized utilities for mapping operations
/// </summary>
public static class MappingUtils
{
    /// <summary>
    /// Determines if a type is a collection type
    /// </summary>
    public static bool IsCollectionType(Type type)
    {
        return type != typeof(string) && 
               (type.IsArray || 
                typeof(System.Collections.IEnumerable).IsAssignableFrom(type));
    }
    
    /// <summary>
    /// Gets the element type of a collection
    /// </summary>
    public static Type? GetCollectionElementType(Type collectionType)
    {
        if (collectionType.IsArray)
        {
            return collectionType.GetElementType();
        }
        
        var genericArgs = collectionType.GetGenericArguments();
        if (genericArgs.Length > 0 && 
            typeof(System.Collections.IEnumerable).IsAssignableFrom(collectionType))
        {
            return genericArgs[0];
        }
        
        return null;
    }
    
    /// <summary>
    /// Creates a destination collection from a list of items
    /// </summary>
    public static object? CreateDestinationCollection(IList<object> items, Type destCollectionType, Type elementType)
    {
        // Handle arrays
        if (destCollectionType.IsArray)
        {
            var array = Array.CreateInstance(elementType, items.Count);
            for (int i = 0; i < items.Count; i++)
            {
                array.SetValue(items[i], i);
            }
            return array;
        }
        
        // Handle generic IEnumerable types by creating a List<T>
        if (destCollectionType.IsGenericType)
        {
            var listType = typeof(List<>).MakeGenericType(elementType);
            var list = Activator.CreateInstance(listType);
            var addMethod = listType.GetMethod("Add");
            
            if (list != null && addMethod != null)
            {
                foreach (var item in items)
                {
                    addMethod.Invoke(list, new[] { item });
                }
                return list;
            }
        }
        
        return null;
    }
    
    /// <summary>
    /// Tries to convert special types that have special conversion rules
    /// </summary>
    public static bool TryConvertSpecialTypes(object value, Type targetType, out object? converted)
    {
        converted = null;
        Type underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;
        
        // Handle Guid conversions
        if (underlyingType == typeof(Guid))
        {
            if (value is string stringValue && Guid.TryParse(stringValue, out var guid))
            {
                converted = guid;
                return true;
            }
        }
        
        // Handle DateTime conversions
        if (underlyingType == typeof(DateTime))
        {
            if (value is string dateString && DateTime.TryParse(dateString, out var date))
            {
                converted = date;
                return true;
            }
        }
        
        // Handle enum conversions
        if (underlyingType.IsEnum)
        {
            if (value is string enumString)
            {
                try
                {
                    converted = Enum.Parse(underlyingType, enumString, true);
                    return true;
                }
                catch { /* Parsing failed */ }
            }
            else if (value is int intValue)
            {
                converted = Enum.ToObject(underlyingType, intValue);
                return true;
            }
        }
        
        return false;
    }
    
    /// <summary>
    /// Determines if a type is nullable
    /// </summary>
    public static bool IsNullableType(Type type)
    {
        return !type.IsValueType || Nullable.GetUnderlyingType(type) != null;
    }
    
    /// <summary>
    /// Determines if a type can be converted to another
    /// </summary>
    public static bool CanConvert(Type sourceType, Type targetType)
    {
        var srcUnderlying = Nullable.GetUnderlyingType(sourceType) ?? sourceType;
        var destUnderlying = Nullable.GetUnderlyingType(targetType) ?? targetType;
        
        return (srcUnderlying == destUnderlying) ||
               (destUnderlying.IsAssignableFrom(srcUnderlying)) ||
               IsNumericType(srcUnderlying) && IsNumericType(destUnderlying);
    }
    
    /// <summary>
    /// Determines if a type is numeric
    /// </summary>
    public static bool IsNumericType(Type type)
    {
        return type == typeof(int) || type == typeof(long) || 
               type == typeof(float) || type == typeof(double) || 
               type == typeof(decimal) || type == typeof(short) || 
               type == typeof(byte) || type == typeof(uint) || 
               type == typeof(ulong) || type == typeof(ushort) || 
               type == typeof(sbyte);
    }
    
    /// <summary>
    /// Maps common properties from source to destination
    /// </summary>
    public static void MapCommonProperties(object source, object destination)
    {
        var sourceProperties = source.GetType().GetProperties();
        var destProperties = destination.GetType().GetProperties()
            .ToDictionary(p => p.Name);
            
        foreach (var sourceProp in sourceProperties)
        {
            if (destProperties.TryGetValue(sourceProp.Name, out var destProp) && 
                destProp.CanWrite)
            {
                try
                {
                    var value = sourceProp.GetValue(source);
                    
                    // Skip null values for value types without nullable wrapper
                    if (value == null && destProp.PropertyType.IsValueType && 
                        Nullable.GetUnderlyingType(destProp.PropertyType) == null)
                    {
                        continue;
                    }
                    
                    // Direct assignment for compatible types
                    if (destProp.PropertyType.IsAssignableFrom(sourceProp.PropertyType))
                    {
                        destProp.SetValue(destination, value);
                        continue;
                    }
                    
                    // Try special conversions
                    if (value != null && TryConvertSpecialTypes(value, destProp.PropertyType, out var converted))
                    {
                        destProp.SetValue(destination, converted);
                        continue;
                    }
                    
                    // Try standard conversion
                    if (value != null && CanConvert(sourceProp.PropertyType, destProp.PropertyType))
                    {
                        var destType = Nullable.GetUnderlyingType(destProp.PropertyType) ?? destProp.PropertyType;
                        var standardConverted = Convert.ChangeType(value, destType);
                        destProp.SetValue(destination, standardConverted);
                    }
                }
                catch
                {
                    // Skip on conversion failure
                }
            }
        }
    }
    
    /// <summary>
    /// Creates an instance of a type with fallback to default for value types
    /// </summary>
    public static T? CreateInstance<T>()
    {
        Type type = typeof(T);
        
        // Handle value types and nullable value types
        if (type.IsValueType)
        {
            return default;
        }
        
        // Try to create instance with parameterless constructor
        try
        {
            if (type.GetConstructor(Type.EmptyTypes) != null)
            {
                return (T)Activator.CreateInstance(type)!;
            }
        }
        catch
        {
            // Fall through to return default
        }
        
        return default;
    }
}