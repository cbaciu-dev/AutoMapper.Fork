namespace BKS.CustomMapper;

/// <summary>
/// Options for mapping configuration
/// </summary>
public class MappingOptions
{
    /// <summary>
    /// Dictionary to store custom mapping items
    /// </summary>
    public Dictionary<string, object> Items { get; } = [];

    /// <summary>
    /// Internal reference to the active mapper
    /// </summary>
    internal Interfaces.IMapper? MapperRef { get; set; }

    /// <summary>
    /// Maps a source object to the specified destination type
    /// </summary>
    public TDestination? Map<TDestination>(object? source)
        => MapperRef != null ? MapperRef.Map<TDestination>(source) : default;
        
    /// <summary>
    /// Maps a source object to the specified destination type
    /// </summary>
    public object? Map(object? source, Type destinationType)
    {
        if (source == null || MapperRef == null)
            return null;
            
        var mapMethod = MapperRef.GetType().GetMethod("Map", new[] { typeof(object), typeof(Action<MappingOptions>) });
        if (mapMethod != null)
        {
            var genericMethod = mapMethod.MakeGenericMethod(destinationType);
            return genericMethod.Invoke(MapperRef, new object[] { 
                source, 
                new Action<MappingOptions>(_ => {}) 
            });
        }
        
        return null;
    }

    /// <summary>
    /// Gets a value from the Items dictionary with type conversion
    /// </summary>
    public T? GetValue<T>(string key)
    {
        if (Items.TryGetValue(key, out var value))
        {
            if (value is T typedValue)
            {
                return typedValue;
            }
            
            try
            {
                if (value != null)
                {
                    // Handle string to Guid conversion
                    if (typeof(T) == typeof(Guid) && value is string stringValue)
                    {
                        if (Guid.TryParse(stringValue, out var guidValue))
                        {
                            return (T)(object)guidValue;
                        }
                    }
                    
                    // Handle other conversions
                    return (T)Convert.ChangeType(value, typeof(T));
                }
            }
            catch
            {
                // Conversion failed
            }
        }
        
        return default;
    }
}
