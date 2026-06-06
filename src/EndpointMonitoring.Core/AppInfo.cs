using System.Reflection;

namespace EndpointMonitoring.Core;

/// <summary>Exposes the application version stamped at build time (see src/Directory.Build.props).</summary>
public static class AppInfo
{
    /// <summary>
    /// Full informational version of the entry assembly, e.g. "2026.1.0606.1432+ab12cd3"
    /// (CalVer YYYY.R.MMdd.HHmm plus short git commit hash) or "2026.1.0.0-dev" for Debug builds.
    /// </summary>
    public static string Version { get; } =
        (Assembly.GetEntryAssembly() ?? typeof(AppInfo).Assembly)
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion ?? "0.0.0";
}
