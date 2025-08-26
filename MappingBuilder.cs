using System.Linq.Expressions;

namespace BKS.CustomMapper;

/// <summary>
/// Fluent builder for configuring mappings
/// </summary>
public class MappingBuilder<TSource, TDestination>
    where TSource : class
    where TDestination : class
{
    private readonly List<Action<TSource, TDestination, MappingOptions>> mappingActions = new();
    private Func<TSource, MappingOptions, TDestination>? customConstructor;

    /// <summary>
    /// Gets whether this builder has any configured mapping actions
    /// </summary>
    internal bool HasMappingActions => mappingActions.Count > 0;

    /// <summary>
    /// Gets whether this builder has a custom constructor
    /// </summary>
    internal bool HasCustomConstructor => customConstructor != null;

    /// <summary>
    /// Configure mapping for a specific destination member
    /// </summary>
    public MappingBuilder<TSource, TDestination> ForMember<TMember>(
        Expression<Func<TDestination, TMember>> destinationMember,
        Func<MemberMappingBuilder<TSource, TDestination, TMember>, MemberMappingBuilder<TSource, TDestination, TMember>> mappingAction)
    {
        var memberExpression = destinationMember.Body as MemberExpression;
        var propertyName = memberExpression?.Member.Name;

        if (propertyName != null)
        {
            var builder = new MemberMappingBuilder<TSource, TDestination, TMember>(propertyName);
            var configuredBuilder = mappingAction(builder);

            mappingActions.Add((source, destination, options) =>
            {
                configuredBuilder.Apply(source, destination, options);
            });
        }

        return this;
    }

    /// <summary>
    /// Configure a precondition that applies to all members
    /// </summary>
    public MappingBuilder<TSource, TDestination> ForAllMembers(
        Func<MemberMappingBuilder<TSource, TDestination, object>, MemberMappingBuilder<TSource, TDestination, object>> mappingAction)
    {
        // Get all writable properties of the destination type
        var destProperties = typeof(TDestination).GetProperties()
            .Where(p => p.CanWrite && p.GetSetMethod() != null);

        foreach (var property in destProperties)
        {
            var builder = new MemberMappingBuilder<TSource, TDestination, object>(property.Name);
            var configuredBuilder = mappingAction(builder);

            mappingActions.Add((source, destination, options) =>
            {
                configuredBuilder.Apply(source, destination, options);
            });
        }

        return this;
    }

    /// <summary>
    /// Configure a custom constructor for the destination type (AutoMapper-compatible signature)
    /// </summary>
    public MappingBuilder<TSource, TDestination> ConstructUsing(Func<TSource, MappingOptions, TDestination> constructor)
    {
        customConstructor = constructor;
        return this;
    }

    /// <summary>
    /// Executes the mapping using custom constructor if available
    /// </summary>
    internal TDestination Build(TSource source, TDestination? destination, MappingOptions options)
    {
        // If we have a custom constructor, use it to create a new instance
        TDestination result;
        if (customConstructor != null)
        {
            result = customConstructor(source, options);
        }
        else
        {
            // If no custom constructor and destination is null, try to create a default instance
            result = destination ?? throw new InvalidOperationException($"Cannot create instance of {typeof(TDestination).Name} - no constructor provided and destination is null");
        }

        // Apply all configured mappings to the result
        foreach (var action in mappingActions)
        {
            action(source, result, options);
        }

        return result;
    }
}
