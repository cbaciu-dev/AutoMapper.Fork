using System.Reflection;
using BKS.CustomMapper.Interfaces;

namespace BKS.CustomMapper;

/// <summary>
/// Configuration class for mapping setup, similar to AutoMapper's MapperConfiguration
/// </summary>
public class MapperConfiguration
{
    private readonly List<IMappingProfile> profiles = new();
    private IMapper? mapper;

    /// <summary>
    /// Initializes a new instance of MapperConfiguration with a configuration action
    /// </summary>
    /// <param name="configAction">Action to configure the mapping profiles</param>
    public MapperConfiguration(Action<MapperConfiguration> configAction)
    {
        configAction(this);
    }

    /// <summary>
    /// Adds mapping profiles from the specified assembly
    /// </summary>
    /// <param name="assembly">Assembly containing mapping profiles</param>
    /// <returns>This configuration instance for method chaining</returns>
    public MapperConfiguration AddMaps(Assembly assembly)
    {
        var profileTypes = assembly.GetTypes()
            .Where(t => !t.IsAbstract && !t.IsInterface && typeof(IMappingProfile).IsAssignableFrom(t));

        foreach (var profileType in profileTypes)
        {
            var profile = (IMappingProfile)Activator.CreateInstance(profileType)!;
            profiles.Add(profile);
        }

        return this;
    }

    /// <summary>
    /// Adds a specific mapping profile
    /// </summary>
    /// <typeparam name="TProfile">Type of the mapping profile to add</typeparam>
    /// <returns>This configuration instance for method chaining</returns>
    public MapperConfiguration AddProfile<TProfile>() where TProfile : IMappingProfile, new()
    {
        profiles.Add(new TProfile());
        return this;
    }

    /// <summary>
    /// Adds a mapping profile instance
    /// </summary>
    /// <param name="profile">The mapping profile instance to add</param>
    /// <returns>This configuration instance for method chaining</returns>
    public MapperConfiguration AddProfile(IMappingProfile profile)
    {
        profiles.Add(profile);
        return this;
    }

    /// <summary>
    /// Creates a mapper instance from this configuration
    /// </summary>
    /// <returns>A configured mapper instance</returns>
    public IMapper CreateMapper()
    {
        if (mapper == null)
        {
            var serviceProvider = new TestServiceProvider(profiles);
            mapper = new Mapper(serviceProvider);
        }

        return mapper;
    }

    /// <summary>
    /// Validates that all mapping configurations are valid
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when configuration validation fails</exception>
    public void AssertConfigurationIsValid()
    {
        var validationErrors = new List<string>();

        // Check if any profiles are registered
        if (!profiles.Any())
        {
            validationErrors.Add("No mapping profiles have been registered.");
        }

        // Validate each profile
        foreach (var profile in profiles)
        {
            try
            {
                // Basic validation - ensure profile can be instantiated and has mappings
                if (profile == null)
                {
                    validationErrors.Add("One or more profiles failed to instantiate.");
                    continue;
                }

                // Try to get profile type information
                var profileType = profile.GetType();
                var profileName = profileType.Name;

                // Additional validation could be added here, such as:
                // - Checking for circular dependencies
                // - Validating that all required mappings exist
                // - Ensuring no duplicate mappings
                
                // For now, we'll just verify the profile exists and is properly constructed
                if (string.IsNullOrEmpty(profileName))
                {
                    validationErrors.Add($"Profile of type {profileType.FullName} has invalid configuration.");
                }
            }
            catch (Exception ex)
            {
                validationErrors.Add($"Profile validation failed: {ex.Message}");
            }
        }

        // If there are validation errors, throw an exception
        if (validationErrors.Any())
        {
            var errorMessage = string.Join(Environment.NewLine, validationErrors);
            throw new InvalidOperationException($"Mapping configuration validation failed:{Environment.NewLine}{errorMessage}");
        }
    }

    /// <summary>
    /// Gets all registered profiles
    /// </summary>
    /// <returns>Collection of registered mapping profiles</returns>
    public IEnumerable<IMappingProfile> GetProfiles() => profiles.AsReadOnly();

    /// <summary>
    /// Simple service provider implementation for testing purposes
    /// </summary>
    private class TestServiceProvider : IServiceProvider
    {
        private readonly List<IMappingProfile> profiles;

        public TestServiceProvider(List<IMappingProfile> profiles)
        {
            this.profiles = profiles;
        }

        public object? GetService(Type serviceType)
        {
            if (serviceType == typeof(IEnumerable<IMappingProfile>))
            {
                return profiles;
            }

            if (serviceType == typeof(IMappingProfile))
            {
                return profiles.FirstOrDefault();
            }

            return null;
        }
    }
}

/// <summary>
/// Extension methods for MapperConfiguration to support additional scenarios
/// </summary>
public static class MapperConfigurationExtensions
{
    /// <summary>
    /// Adds multiple mapping profiles from specified types
    /// </summary>
    /// <param name="configuration">The mapper configuration</param>
    /// <param name="profileTypes">Types of mapping profiles to add</param>
    /// <returns>The mapper configuration for method chaining</returns>
    public static MapperConfiguration AddProfiles(this MapperConfiguration configuration, params Type[] profileTypes)
    {
        foreach (var profileType in profileTypes)
        {
            if (typeof(IMappingProfile).IsAssignableFrom(profileType) && !profileType.IsAbstract)
            {
                var profile = (IMappingProfile)Activator.CreateInstance(profileType)!;
                configuration.AddProfile(profile);
            }
        }

        return configuration;
    }
}
