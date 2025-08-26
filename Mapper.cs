using System.Linq.Expressions;
using System.Reflection;
using BKS.CustomMapper.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace BKS.CustomMapper;

/// <summary>
/// Implementation of IMapper that uses manual mapping profiles.
/// </summary>
public class Mapper : IMapper
{
    private readonly IServiceProvider serviceProvider;
    private readonly Dictionary<(Type SourceType, Type DestType), Delegate> mappers = new();

    public Mapper(IServiceProvider serviceProvider) => this.serviceProvider = serviceProvider;

    /// <summary>
    /// Maps an object to the specified destination type.
    /// </summary>
    public TDestination? Map<TDestination>(object? source)
    {
        return Map<TDestination>(source, _ => { });
    }

    /// <summary>
    /// Maps an object to the specified destination type with mapping options.
    /// </summary>
    public TDestination? Map<TDestination>(object? source, Action<MappingOptions> configAction)
    {
        if (source == null)
        {
            return default;
        }

        var sourceType = source.GetType();
        var destType = typeof(TDestination);

        // Create mapping options for context passing
        var options = new MappingOptions
        {
            // expose mapper for nested/element mapping
            MapperRef = this
        };
        configAction(options);

        // Find and execute the appropriate mapping function
        if (mappers.TryGetValue((sourceType, destType), out var mappingFunc))
        {
            // Use the cached mapper function if it exists
            return ((Func<object, MappingOptions, TDestination>)mappingFunc)(source, options);
        }

        // Try to find a profile that can handle this mapping
        var profiles = serviceProvider.GetServices<IMappingProfile>();
        var profile = profiles.FirstOrDefault(p => p.CanMap(sourceType, destType));
        if (profile != null)
        {
            // Create destination - this might be null for types without parameterless constructors
            var destination = MappingUtils.CreateInstance<TDestination>();
            
            // Execute the mapping using the overload that provides destination type info
            var result = ((MappingProfile)profile).Map(source, destination, destType, options);
            return (TDestination)result;
        }

        // If we reach here, no mapping profile was found.
        // For simple property-to-property mapping, create a destination and use AutoMap
        var fallbackDestination = MappingUtils.CreateInstance<TDestination>();
        if (fallbackDestination != null)
        {
            return AutoMap(source, fallbackDestination);
        }

        return default;
    }

    /// <summary>
    /// Maps a source object to an existing destination object.
    /// </summary>
    public TDestination Map<TSource, TDestination>(TSource? source, TDestination destination)
    {
        if (source == null)
        {
            return destination;
        }

        var sourceType = typeof(TSource);
        var destType = typeof(TDestination);

        // Create mapping options with default values
        var options = new MappingOptions
        {
            MapperRef = this
        };

        // Find and execute the appropriate mapping function
        if (mappers.TryGetValue((sourceType, destType), out var mappingFunc))
        {
            // Use the cached mapper function if it exists
            return ((Func<TSource, TDestination, MappingOptions, TDestination>)mappingFunc)(source, destination, options);
        }

        // Try to find a profile that can handle this mapping
        var profiles = serviceProvider.GetServices<IMappingProfile>();
        var profile = profiles.FirstOrDefault(p => p.CanMap(sourceType, destType));
        if (profile != null)
        {
            // Execute the mapping and return the result
            return (TDestination)((MappingProfile)profile).Map(source!, destination, destType, options);
        }

        // If no mapping profile is found, use property-based mapping
        return AutoMap(source, destination);
    }

    /// <summary>
    /// Registers a mapping function for a source and destination type.
    /// </summary>
    public void RegisterMapping<TSource, TDestination>(Func<TSource, MappingOptions, TDestination> mappingFunc)
    {
        mappers[(typeof(TSource), typeof(TDestination))] = mappingFunc;
    }

    /// <summary>
    /// Projects a queryable of TSource to a queryable of TDestination
    /// </summary>
    public IQueryable<TDestination> ProjectTo<TDestination>(IQueryable source)
    {
        if (source == null)
            return Enumerable.Empty<TDestination>().AsQueryable();

        // Get the source element type from the queryable
        var sourceElementType = source.ElementType;
        var destType = typeof(TDestination);

        // Create a parameter expression for the source type
        var parameter = Expression.Parameter(sourceElementType, "src");
        
        // Get destination properties
        var destProperties = destType.GetProperties()
            .Where(p => p.CanWrite)
            .ToList();

        // Get source properties for mapping
        var sourceProperties = sourceElementType.GetProperties()
            .ToDictionary(p => p.Name, p => p, StringComparer.OrdinalIgnoreCase);

        // Create member bindings for the destination object
        var bindings = new List<MemberBinding>();

        // Try to find a profile that can handle this mapping for custom mappings (not yet expression based)
        var profiles = serviceProvider.GetServices<IMappingProfile>();
        var profile = profiles.FirstOrDefault(p => p.CanMap(sourceElementType, destType));

        foreach (var destProp in destProperties)
        {
            Expression? valueExpression = null;

            // Special mapping: ExternalId -> Id
            if (string.Equals(destProp.Name, "Id", StringComparison.OrdinalIgnoreCase))
            {
                if (sourceProperties.TryGetValue("ExternalId", out var srcExtId))
                {
                    var srcExpr = Expression.Property(parameter, srcExtId);
                    valueExpression = BuildConversionExpression(srcExpr, srcExtId.PropertyType, destProp.PropertyType);
                }
            }

            // Direct property name mapping
            if (valueExpression == null && sourceProperties.TryGetValue(destProp.Name, out var matchingSourceProp))
            {
                var srcExpr = Expression.Property(parameter, matchingSourceProp);
                valueExpression = BuildConversionExpression(srcExpr, matchingSourceProp.PropertyType, destProp.PropertyType);
            }

            // If a value expression was created, bind it
            if (valueExpression != null)
            {
                bindings.Add(Expression.Bind(destProp, valueExpression));
            }
        }

        // Create the member initialization expression
        var newExpression = Expression.New(destType);
        var memberInit = Expression.MemberInit(newExpression, bindings);

        // Create a typed lambda: Expression<Func<TSource, TDestination>>
        var funcType = typeof(Func<,>).MakeGenericType(sourceElementType, destType);
        var typedLambda = Expression.Lambda(funcType, memberInit, parameter);

        // Use reflection to call the generic Select method
        var selectMethod = typeof(Queryable).GetMethods()
            .First(m => m.Name == "Select" && m.GetParameters().Length == 2)
            .MakeGenericMethod(sourceElementType, destType);

        var result = selectMethod.Invoke(null, new object[] { source, typedLambda })!;
        return (IQueryable<TDestination>)result;
    }

    /// <summary>
    /// Builds an expression that converts a source expression to the destination type when possible
    /// </summary>
    private static Expression? BuildConversionExpression(Expression sourceExpr, Type sourceType, Type destType)
    {
        // Exact match
        if (destType.IsAssignableFrom(sourceType))
        {
            return sourceExpr;
        }

        // Nullable handling
        var srcUnderlying = Nullable.GetUnderlyingType(sourceType) ?? sourceType;
        var destUnderlying = Nullable.GetUnderlyingType(destType) ?? destType;

        if (srcUnderlying == destUnderlying)
        {
            // e.g. Guid -> Guid? or Guid? -> Guid?
            if (destType != sourceType)
            {
                return Expression.Convert(sourceExpr, destType);
            }
            return sourceExpr;
        }

        // String conversions for common primitives (Guid, DateTime, etc.)
        if (destUnderlying == typeof(string))
        {
            // Handle nullable source: src == null ? null : src.ToString()
            if (Nullable.GetUnderlyingType(sourceType) != null)
            {
                var hasValueProp = Expression.Property(sourceExpr, "HasValue");
                var valueProp = Expression.Property(sourceExpr, "Value");
                var toStringCall = Expression.Call(valueProp, valueProp.Type.GetMethod("ToString", Type.EmptyTypes)!);
                var nullConst = Expression.Constant(null, typeof(string));
                var condition = Expression.Condition(hasValueProp, toStringCall, nullConst);
                return condition;
            }

            var toStringMethod = sourceType.GetMethod("ToString", Type.EmptyTypes);
            if (toStringMethod != null)
            {
                return Expression.Call(sourceExpr, toStringMethod);
            }
        }

        // Numeric conversions or value conversions where CLR/EF can translate a Convert
        try
        {
            // Convert(sourceExpr) to destUnderlying, then to destType (to re-wrap nullable)
            var convertedUnderlying = Expression.Convert(sourceExpr, destUnderlying);
            if (destType != destUnderlying)
            {
                return Expression.Convert(convertedUnderlying, destType);
            }
            return convertedUnderlying;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Simple property-to-property mapping for when no profile exists.
    /// </summary>
    private TDestination? AutoMap<TSource, TDestination>(TSource? source, TDestination? destination)
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
                    
                    // Handle direct type assignments and type compatibility
                    if (destProp.PropertyType == sourceProp.PropertyType || 
                        destProp.PropertyType.IsAssignableFrom(sourceProp.PropertyType))
                    {
                        if (value != null || MappingUtils.IsNullableType(destProp.PropertyType))
                        {
                            destProp.SetValue(destination, value);
                        }
                    }
                    // Handle nullable conversions and special types
                    else if (value != null)
                    {
                        if (MappingUtils.TryConvertSpecialTypes(value, destProp.PropertyType, out var convertedValue))
                        {
                            destProp.SetValue(destination, convertedValue);
                        }
                        else if (MappingUtils.CanConvert(sourceProp.PropertyType, destProp.PropertyType))
                        {
                            var standardConverted = Convert.ChangeType(value, destProp.PropertyType);
                            destProp.SetValue(destination, standardConverted);
                        }
                    }
                }
                catch
                {
                    // Skip properties that fail to map
                }
            }
        }

        return destination;
    }

    /// <summary>
    /// Maps collections from source to destination type
    /// </summary>
    private object? MapCollection(object sourceCollection, Type sourceType, Type destType)
    {
        try
        {
            // Get the element types
            var sourceElementType = MappingUtils.GetCollectionElementType(sourceType);
            var destElementType = MappingUtils.GetCollectionElementType(destType);
            
            if (sourceElementType == null || destElementType == null)
                return null;

            // Convert source to enumerable
            var sourceEnumerable = (System.Collections.IEnumerable)sourceCollection;
            var mappedItems = new List<object>();

            foreach (var item in sourceEnumerable)
            {
                if (item != null)
                {
                    // Try to map each item
                    var mappedItem = Map<object>(item);
                    if (mappedItem != null && destElementType.IsAssignableFrom(mappedItem.GetType()))
                    {
                        mappedItems.Add(mappedItem);
                    }
                    else if (sourceElementType == destElementType)
                    {
                        mappedItems.Add(item);
                    }
                }
            }

            // Create the destination collection using utility method
            return MappingUtils.CreateDestinationCollection(mappedItems, destType, destElementType);
        }
        catch
        {
            // If collection mapping fails, return null
        }

        return null;
    }
}