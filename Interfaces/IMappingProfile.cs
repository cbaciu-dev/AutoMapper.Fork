namespace BKS.CustomMapper.Interfaces;

/// <summary>
/// Interface for mapping profiles
/// </summary>
public interface IMappingProfile
{
    /// <summary>
    /// Determines if this profile can map between the source and destination types
    /// </summary>
    bool CanMap(Type sourceType, Type destinationType);

    /// <summary>
    /// Maps a source object to a destination object
    /// </summary>
    object Map(object source, object destination, MappingOptions options);
}