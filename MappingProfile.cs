using System.Globalization;
using System.Reflection;
using BKS.CustomMapper.Interfaces;

namespace BKS.CustomMapper;

/// <summary>
/// Base class for mapping profiles.
/// </summary>
public abstract class MappingProfile : IMappingProfile
{
    protected readonly Dictionary<(Type SourceType, Type DestType), Func<object, object, MappingOptions, object>> mappingFunctions = new();

    protected MappingProfile()
    {
        ConfigureMappings();
    }

    /// <summary>
    /// Configure mappings in derived classes
    /// </summary>
    protected abstract void ConfigureMappings();

    /// <summary>
    /// Determines if this profile can map between the source and destination types
    /// </summary>
    public bool CanMap(Type sourceType, Type destinationType)
    {
        return mappingFunctions.ContainsKey((sourceType, destinationType));
    }

    /// <summary>
    /// Maps a source object to a destination object
    /// </summary>
    public object Map(object source, object? destination, MappingOptions options)
    {
        var sourceType = source.GetType();
        
        // We need to determine the destination type from either:
        // 1. The destination object if it's not null
        // 2. The calling context (this is tricky since we don't have direct access)
        
        // Try to find mapping by looking at the specific source-destination type combination
        // If destination is not null, use its type
        if (destination != null)
        {
            var destType = destination.GetType();
            if (mappingFunctions.TryGetValue((sourceType, destType), out var specificMapping))
            {
                return specificMapping(source, destination, options);
            }
        }
        
        // If destination is null or no specific mapping found, we need to find by source type
        // This is problematic when there are multiple mappings from the same source type
        // For now, let's return the destination or throw an error
        if (destination == null)
        {
            throw new InvalidOperationException($"Cannot find mapping for source type {sourceType.Name} without a destination instance or type hint");
        }

        return destination;
    }

    /// <summary>
    /// Maps a source object to a destination object with known destination type
    /// </summary>
    public object Map(object source, object? destination, Type destinationType, MappingOptions options)
    {
        var sourceType = source.GetType();
        
        if (mappingFunctions.TryGetValue((sourceType, destinationType), out var mappingFunc))
        {
            return mappingFunc(source, destination!, options);
        }

        // If no specific mapping found, return destination or throw
        if (destination == null)
        {
            throw new InvalidOperationException($"Cannot find mapping for source type {sourceType.Name} to destination type {destinationType.Name}");
        }

        return destination;
    }

    /// <summary>
    /// Creates a mapping between source and destination types
    /// </summary>
    protected void CreateMap<TSource, TDestination>()
        where TSource : class
        where TDestination : class
    {
        // Register a simple auto-mapping function
        mappingFunctions[(typeof(TSource), typeof(TDestination))] = (source, destination, options) =>
        {
            return AutoMap((TSource)source, (TDestination)destination);
        };
    }

    /// <summary>
    /// Creates a mapping between source and destination types with configuration
    /// </summary>
    protected void CreateMap<TSource, TDestination>(Action<MappingBuilder<TSource, TDestination>> configAction)
        where TSource : class
        where TDestination : class
    {
        var builder = new MappingBuilder<TSource, TDestination>();
        configAction(builder);

        // Register the mapping function
        mappingFunctions[(typeof(TSource), typeof(TDestination))] = (source, destination, options) =>
        {
            // Cast the parameters
            var typedSource = (TSource)source;
            var typedDestination = (TDestination?)destination;

            // Use the builder to handle both custom constructors and property mappings
            var result = builder.Build(typedSource, typedDestination!, options);
            
            // Always apply AutoMap for any properties not explicitly configured
            // This ensures that properties like Name get copied over after construction
            result = AutoMap(typedSource, result);
            
            return result;
        };
    }

    /// <summary>
    /// Creates a mapping for projection (for query scenarios)
    /// </summary>
    protected void CreateProjection<TSource, TDestination>(Action<MappingBuilder<TSource, TDestination>> configAction)
        where TSource : class
        where TDestination : class
    {
        // For projections, we use the same implementation as CreateMap
        CreateMap(configAction);
    }

    /// <summary>
    /// Creates a bidirectional mapping between source and destination types
    /// </summary>
    protected void CreateMap<TSource, TDestination>(bool bidirectional)
        where TSource : class
        where TDestination : class
    {
        // Create the forward mapping
        CreateMap<TSource, TDestination>();
        
        // Create the reverse mapping if bidirectional is true
        if (bidirectional)
        {
            CreateMap<TDestination, TSource>();
        }
    }

    /// <summary>
    /// Creates a bidirectional mapping between source and destination types with configuration
    /// </summary>
    protected void CreateMap<TSource, TDestination>(Action<MappingBuilder<TSource, TDestination>> configAction, bool bidirectional)
        where TSource : class
        where TDestination : class
    {
        // Create the forward mapping
        CreateMap(configAction);
        
        // Create the reverse mapping if bidirectional is true
        if (bidirectional)
        {
            CreateMap<TDestination, TSource>();
        }
    }

    /// <summary>
    /// Simple property-to-property mapping with enhanced type support
    /// </summary>
    private TDestination AutoMap<TSource, TDestination>(TSource source, TDestination destination)
        where TSource : class
        where TDestination : class
    {
        if (source == null || destination == null)
        {
            return destination;
        }

        var sourceProperties = typeof(TSource).GetProperties(BindingFlags.Public | BindingFlags.Instance);
        var destProperties = typeof(TDestination).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .ToDictionary(p => p.Name);

        foreach (var sourceProp in sourceProperties)
        {
            if (destProperties.TryGetValue(sourceProp.Name, out var destProp) &&
                destProp.CanWrite)
            {
                try
                {
                    var value = sourceProp.GetValue(source);
                    
                    // Handle direct type assignments (like Guid to Guid)
                    if (destProp.PropertyType == sourceProp.PropertyType)
                    {
                        if (value != null || MappingUtils.IsNullableType(destProp.PropertyType))
                        {
                            destProp.SetValue(destination, value);
                        }
                    }
                    // Handle type compatibility
                    else if (destProp.PropertyType.IsAssignableFrom(sourceProp.PropertyType))
                    {
                        if (value != null || MappingUtils.IsNullableType(destProp.PropertyType))
                        {
                            destProp.SetValue(destination, value);
                        }
                    }
                    // Handle special type conversions
                    else if (value != null && MappingUtils.TryConvertSpecialTypes(value, destProp.PropertyType, out var convertedValue))
                    {
                        destProp.SetValue(destination, convertedValue);
                    }
                    // Handle nullable conversions
                    else if (value != null && MappingUtils.CanConvert(sourceProp.PropertyType, destProp.PropertyType))
                    {
                        var standardConverted = Convert.ChangeType(value, destProp.PropertyType);
                        destProp.SetValue(destination, standardConverted);
                    }
                }
                catch
                {
                    // Skip if conversion fails
                }
            }
        }

        return destination;
    }

    /// <summary>
    /// Maps collection elements automatically when collection properties have the same name
    /// </summary>
    private object? MapCollectionElements(object sourceCollection, Type sourceType, Type destType)
    {
        try
        {
            // Get the element types
            var sourceElementType = MappingUtils.GetCollectionElementType(sourceType);
            var destElementType = MappingUtils.GetCollectionElementType(destType);
            
            if (sourceElementType == null || destElementType == null)
                return sourceCollection; // Return as-is if we can't determine element types

            // Convert source to enumerable
            var sourceEnumerable = (System.Collections.IEnumerable)sourceCollection;
            var mappedItems = new List<object>();

            foreach (var item in sourceEnumerable)
            {
                if (item != null)
                {
                    object? mappedItem = null;
                    
                    // If element types are the same, use directly
                    if (sourceElementType == destElementType)
                    {
                        mappedItem = item;
                    }
                    else
                    {
                        // Try to map using the mapper instance
                        try
                        {
                            mappedItem = MapElementToDestinationType(item, destElementType);
                        }
                        catch
                        {
                            // If mapping fails, try direct instantiation and property copying
                            mappedItem = CreateAndMapElementManually(item, destElementType);
                        }
                    }
                    
                    if (mappedItem != null)
                    {
                        mappedItems.Add(mappedItem);
                    }
                }
            }

            // Create the destination collection using utility method
            return MappingUtils.CreateDestinationCollection(mappedItems, destType, destElementType);
        }
        catch
        {
            return sourceCollection; // Return original if mapping fails
        }
    }

    /// <summary>
    /// Maps a single element to the destination type using the mapper
    /// </summary>
    private object? MapElementToDestinationType(object sourceElement, Type destElementType)
    {
        // Use reflection to call the generic Map method
        var mapMethod = GetType().GetMethod(nameof(Map), new[] { typeof(object), typeof(Action<MappingOptions>) });
        if (mapMethod != null)
        {
            var genericMethod = mapMethod.MakeGenericMethod(destElementType);
            return genericMethod.Invoke(this, new object[] { sourceElement, new Action<MappingOptions>(_ => { }) });
        }
        
        return null;
    }

    /// <summary>
    /// Creates and manually maps an element when automatic mapping fails
    /// </summary>
    private object? CreateAndMapElementManually(object sourceElement, Type destElementType)
    {
        try
        {
            // Try to create destination element instance
            if (destElementType.GetConstructor(Type.EmptyTypes) != null)
            {
                var destElement = Activator.CreateInstance(destElementType);
                if (destElement != null)
                {
                    // Map properties from source to destination using utility method
                    MappingUtils.MapCommonProperties(sourceElement, destElement);
                    return destElement;
                }
            }
        }
        catch
        {
            // If we can't create the destination type, return null
        }
        
        return null;
    }

    /// <summary>
    /// Helper method to safely get typed values from context items
    /// </summary>
    protected static T GetContextValue<T>(MappingOptions options, string key)
    {
        if (options.Items.TryGetValue(key, out var value))
        {
            if (value is T typedValue)
            {
                return typedValue;
            }
            
            if (value != null)
            {
                // Handle string to Guid conversion specifically
                if (typeof(T) == typeof(Guid) && value is string stringValue)
                {
                    if (Guid.TryParse(stringValue, out var guidValue))
                    {
                        return (T)(object)guidValue;
                    }
                }
                
                // Handle other type conversions
                try
                {
                    return (T)Convert.ChangeType(value, typeof(T));
                }
                catch
                {
                    // If conversion fails, return default
                    return default(T)!;
                }
            }
        }
        
        return default(T)!;
    }
}