using Microsoft.Extensions.Logging;

namespace AutoMapper.Licensing;

internal class LicenseValidator
{
    private readonly ILogger _logger;

    public LicenseValidator(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger("LuckyPennySoftware.AutoMapper.License");
    }

    public void Validate(License license)
    {
        var errors = new List<string>();

        if (license is not { IsConfigured: true })
        {
            var message = "You do not have a valid license key for the Lucky Penny software AutoMapper. " +
                          "This is allowed for development and testing scenarios. " +
                          "If you are running in production you are required to have a licensed version. " +
                          "Please visit https://luckypennysoftware.com to obtain a valid license.";

            _logger.LogWarning(message);
            return;
        }

        _logger.LogDebug("The Lucky Penny license key details: {license}", license);

        var diff = DateTime.UtcNow.Date.Subtract(license.ExpirationDate.Value.Date).TotalDays;
        if (diff > 0)
        {
            errors.Add($"Your license for the Lucky Penny software AutoMapper expired {diff} days ago.");
        }

        if (license.ProductType.Value != ProductType.AutoMapper
            && license.ProductType.Value != ProductType.Bundle)
        {
            errors.Add("Your Lucky Penny software license does not include AutoMapper.");
        }

        if (errors.Count > 0)
        {
            foreach (var err in errors)
            {
                _logger.LogError(err);
            }

            _logger.LogError(
                "Please visit https://luckypennysoftware.com to obtain a valid license for the Lucky Penny software AutoMapper.");
        }
        else
        {
            _logger.LogInformation("You have a valid license key for the Lucky Penny software {type} {edition} edition. The license expires on {licenseExpiration}.",
                license.ProductType,
                license.Edition,
                license.ExpirationDate);
        }
    }
}