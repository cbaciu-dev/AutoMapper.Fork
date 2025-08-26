namespace BKS.CustomMapper.Interfaces;

/// <summary>
/// Interface for mapping objects between different types
/// </summary>
public interface IMapper
{
    /// <summary>
    /// Maps an object of type TSource to an object of type TDestination
    /// </summary>
    /// <typeparam name="TDestination">The type to map to</typeparam>
    /// <param name="source">The object to map from</param>
    /// <returns>A mapped object of type TDestination</returns>
    TDestination? Map<TDestination>(object? source);

    /// <summary>
    /// Maps an object of type TSource to an object of type TDestination with additional options
    /// </summary>
    /// <typeparam name="TDestination">The type to map to</typeparam>
    /// <param name="source">The object to map from</param>
    /// <param name="configAction">Action to configure mapping options</param>
    /// <returns>A mapped object of type TDestination</returns>
    TDestination? Map<TDestination>(object? source, Action<MappingOptions> configAction);

    /// <summary>
    /// Maps an object of type TSource to an existing object of type TDestination
    /// </summary>
    /// <typeparam name="TSource">The type to map from</typeparam>
    /// <typeparam name="TDestination">The type to map to</typeparam>
    /// <param name="source">The object to map from</param>
    /// <param name="destination">The object to map to</param>
    /// <returns>The updated destination object</returns>
    TDestination Map<TSource, TDestination>(TSource? source, TDestination destination);

    /// <summary>
    /// Projects a queryable of TSource to a queryable of TDestination
    /// </summary>
    /// <typeparam name="TDestination">The type to project to</typeparam>
    /// <param name="source">The queryable to project from</param>
    /// <returns>A queryable of the destination type</returns>
    IQueryable<TDestination> ProjectTo<TDestination>(IQueryable source);
}