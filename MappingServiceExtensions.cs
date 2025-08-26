using System.Reflection;
using BKS.CustomMapper.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace BKS.CustomMapper;

/// <summary>
/// Extension methods for mapping configuration
/// </summary>
public static class MappingServiceExtensions
{
    /// <summary>
    /// Adds mapping services to the service collection
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="assembly">The assembly containing mapping profiles</param>
    /// <returns>The service collection</returns>
    public static IServiceCollection AddMappingServices(this IServiceCollection services, Assembly assembly)
    {
        // Register the mapper as a singleton
        services.AddSingleton<IMapper, Mapper>();

        // Find all mapping profile implementations in the assembly and register them
        var profileTypes = assembly.GetTypes()
            .Where(t => !t.IsAbstract && !t.IsInterface && typeof(IMappingProfile).IsAssignableFrom(t));

        foreach (var profileType in profileTypes)
        {
            services.AddSingleton(typeof(IMappingProfile), profileType);
        }

        return services;
    }
}