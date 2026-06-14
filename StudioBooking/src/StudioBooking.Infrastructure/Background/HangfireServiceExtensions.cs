using Hangfire;
using Hangfire.MySql;

namespace StudioBooking.Infrastructure.Background;

public static class HangfireServiceExtensions
{
    public static IGlobalConfiguration ConfigureHangfireStorage(this IGlobalConfiguration configuration, string connectionString)
    {
        // Hangfire.MySqlStorage uses @rownum user variables in SQL (see GetRangeFromSet).
        var hangfireConnectionString = EnsureAllowUserVariables(connectionString);

        return configuration
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UseStorage(new MySqlStorage(hangfireConnectionString, new MySqlStorageOptions()));
    }

    private static string EnsureAllowUserVariables(string connectionString)
    {
        if (connectionString.Contains("Allow User Variables", StringComparison.OrdinalIgnoreCase))
            return connectionString;

        return connectionString.TrimEnd(';') + ";Allow User Variables=true";
    }
}
