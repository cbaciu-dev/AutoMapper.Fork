using System.Linq.Expressions;

namespace BKS.CustomMapper;

/// <summary>
/// Builder for configuring member mapping.
/// </summary>
public class MemberMappingBuilder<TSource, TDestination, TMember>
    where TSource : class
    where TDestination : class
{
    private readonly string propertyName;
    private Func<TSource, MappingOptions, TMember>? valueSelector;
    private Func<TSource, TDestination, TMember, MappingOptions, TMember>? fullValueSelector;
    private Func<TSource, TDestination, TMember, MappingOptions, object?>? objectValueSelector;
    private Func<MappingOptions, bool>? preCondition;
    private Func<TSource, TDestination, bool>? sourceDestPreCondition;
    private bool ignore;

    public MemberMappingBuilder(string propertyName)
    {
        this.propertyName = propertyName;
    }

    /// <summary>
    /// Maps the member from a source expression
    /// </summary>
    public MemberMappingBuilder<TSource, TDestination, TMember> MapFrom<TSourceMember>(
        Expression<Func<TSource, TSourceMember>> sourceMember)
    {
        var compiledFunc = sourceMember.Compile();
        objectValueSelector = (source, destination, currentValue, options) =>
        {
            try
            {
                return compiledFunc(source);
            }
            catch
            {
                return default(TMember)!;
            }
        };
        return this;
    }

    /// <summary>
    /// Maps the member using a custom function with full context access
    /// </summary>
    public MemberMappingBuilder<TSource, TDestination, TMember> MapFrom(
        Func<TSource, TDestination, TMember, MappingOptions, TMember> valueSelector)
    {
        this.fullValueSelector = valueSelector;
        return this;
    }

    /// <summary>
    /// Maps the member using a custom function that returns object
    /// </summary>
    public MemberMappingBuilder<TSource, TDestination, TMember> MapFrom(
        Func<TSource, TDestination, TMember, MappingOptions, object?> valueSelector)
    {
        this.objectValueSelector = valueSelector;
        return this;
    }

    /// <summary>
    /// Maps the member using a resolver
    /// </summary>
    public MemberMappingBuilder<TSource, TDestination, TMember> MapFrom<TResolver>()
        where TResolver : class, new()
    {
        objectValueSelector = (source, dest, currentValue, options) =>
        {
            try
            {
                var resolver = new TResolver();
                var resolverType = typeof(TResolver);
                
                // Try method with (TSource, TDestination, TMember, MappingOptions) signature
                var method = resolverType.GetMethod("Resolve", new[] { 
                    typeof(TSource), typeof(TDestination), typeof(TMember), typeof(MappingOptions) 
                });
                if (method != null)
                {
                    return method.Invoke(resolver, new object[] { source, dest, currentValue!, options });
                }
                
                // Try method with (TSource, TDestination) signature
                method = resolverType.GetMethod("Resolve", new[] { typeof(TSource), typeof(TDestination) });
                if (method != null)
                {
                    return method.Invoke(resolver, new object[] { source, dest });
                }
                
                // Try method with just TSource signature
                method = resolverType.GetMethod("Resolve", new[] { typeof(TSource) });
                if (method != null)
                {
                    return method.Invoke(resolver, new object[] { source });
                }
                
                return default(TMember);
            }
            catch
            {
                return default(TMember);
            }
        };
        
        return this;
    }

    /// <summary>
    /// Sets a precondition for this mapping (context-based)
    /// </summary>
    public MemberMappingBuilder<TSource, TDestination, TMember> PreCondition(
        Func<MappingOptions, bool> condition)
    {
        this.preCondition = condition;
        return this;
    }

    /// <summary>
    /// Sets a precondition for this mapping (source and destination-based)
    /// </summary>
    public MemberMappingBuilder<TSource, TDestination, TMember> PreCondition(
        Func<TSource, TDestination, bool> condition)
    {
        this.sourceDestPreCondition = condition;
        return this;
    }

    /// <summary>
    /// Ignore this member when mapping
    /// </summary>
    public MemberMappingBuilder<TSource, TDestination, TMember> Ignore()
    {
        ignore = true;
        return this;
    }

    /// <summary>
    /// Applies the mapping to the destination object
    /// </summary>
    internal void Apply(TSource source, TDestination destination, MappingOptions options)
    {
        if (ignore || !ShouldApplyMapping(source, destination, options))
        {
            return;
        }

        var destProperty = typeof(TDestination).GetProperty(propertyName);
        if (destProperty != null && destProperty.CanWrite)
        {
            try
            {
                object? value = GetMappedValue(source, destination, options, destProperty);
                
                if (value == null)
                {
                    if (MappingUtils.IsNullableType(destProperty.PropertyType))
                    {
                        destProperty.SetValue(destination, null);
                    }
                    return;
                }

                // Handle collections
                if (MappingUtils.IsCollectionType(value.GetType()) && 
                    MappingUtils.IsCollectionType(destProperty.PropertyType))
                {
                    HandleCollectionMapping(value, destProperty, destination, options);
                    return;
                }

                // Handle complex objects via mapper
                if (ShouldMapComplexObject(value, destProperty))
                {
                    if (TryMapComplexObject(value, destProperty, destination, options))
                    {
                        return;
                    }
                }

                // Standard conversion
                var convertedValue = ConvertValue(value, destProperty.PropertyType);
                destProperty.SetValue(destination, convertedValue);
            }
            catch
            {
                // Skip on failure
            }
        }
    }

    private bool ShouldApplyMapping(TSource source, TDestination destination, MappingOptions options)
    {
        if (preCondition != null)
        {
            try
            {
                if (!preCondition(options))
                {
                    return false;
                }
            }
            catch
            {
                return false;
            }
        }

        if (sourceDestPreCondition != null)
        {
            try
            {
                if (!sourceDestPreCondition(source, destination))
                {
                    return false;
                }
            }
            catch
            {
                return false;
            }
        }

        return true;
    }

    private object? GetMappedValue(TSource source, TDestination destination, MappingOptions options, 
                                  System.Reflection.PropertyInfo destProperty)
    {
        if (objectValueSelector != null)
        {
            var currentValue = destProperty.GetValue(destination);
            var typedCurrentValue = currentValue is TMember current ? current : default(TMember)!;
            return objectValueSelector(source, destination, typedCurrentValue, options);
        }
        
        if (fullValueSelector != null)
        {
            var currentValue = destProperty.GetValue(destination);
            var typedCurrentValue = currentValue is TMember current ? current : default(TMember)!;
            return fullValueSelector(source, destination, typedCurrentValue, options);
        }
        
        if (valueSelector != null)
        {
            return valueSelector(source, options);
        }
        
        return null;
    }

    private void HandleCollectionMapping(object value, System.Reflection.PropertyInfo destProperty, 
                                        TDestination destination, MappingOptions options)
    {
        try
        {
            var sourceType = value.GetType();
            var destType = destProperty.PropertyType;
            
            var sourceElementType = MappingUtils.GetCollectionElementType(sourceType);
            var destElementType = MappingUtils.GetCollectionElementType(destType);
            
            if (sourceElementType == null || destElementType == null)
            {
                destProperty.SetValue(destination, value);
                return;
            }

            var sourceEnumerable = (System.Collections.IEnumerable)value;
            var mappedItems = new List<object>();

            foreach (var item in sourceEnumerable)
            {
                if (item == null) continue;

                object? mappedItem = null;

                if (sourceElementType == destElementType)
                {
                    mappedItem = item;
                }
                else if (options.MapperRef != null)
                {
                    try
                    {
                        mappedItem = options.Map(item, destElementType);
                    }
                    catch
                    {
                        // If mapping fails, try simple property mapping
                        if (destElementType.GetConstructor(Type.EmptyTypes) != null)
                        {
                            mappedItem = Activator.CreateInstance(destElementType);
                            if (mappedItem != null)
                            {
                                MappingUtils.MapCommonProperties(item, mappedItem);
                            }
                        }
                    }
                }

                if (mappedItem != null)
                {
                    mappedItems.Add(mappedItem);
                }
            }

            var result = MappingUtils.CreateDestinationCollection(mappedItems, destType, destElementType);
            if (result != null)
            {
                destProperty.SetValue(destination, result);
            }
        }
        catch
        {
            // If collection mapping fails, try direct assignment
            destProperty.SetValue(destination, value);
        }
    }

    private bool ShouldMapComplexObject(object value, System.Reflection.PropertyInfo destProperty)
    {
        return !MappingUtils.IsCollectionType(value.GetType()) && 
               !destProperty.PropertyType.IsAssignableFrom(value.GetType());
    }

    private bool TryMapComplexObject(object value, System.Reflection.PropertyInfo destProperty, 
                                    TDestination destination, MappingOptions options)
    {
        if (options.MapperRef == null) return false;

        try
        {
            var mappedObj = options.Map(value, destProperty.PropertyType);
            if (mappedObj != null)
            {
                destProperty.SetValue(destination, mappedObj);
                return true;
            }
        }
        catch
        {
            // Fall back to standard conversion
        }

        return false;
    }

    private object? ConvertValue(object value, Type targetType)
    {
        // Handle direct assignment
        if (targetType.IsAssignableFrom(value.GetType()))
        {
            return value;
        }

        // Try special type conversions
        if (MappingUtils.TryConvertSpecialTypes(value, targetType, out var converted))
        {
            return converted;
        }

        // Standard conversion
        try
        {
            var underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;
            return Convert.ChangeType(value, underlyingType);
        }
        catch
        {
            return value; // Return original if conversion fails
        }
    }
}