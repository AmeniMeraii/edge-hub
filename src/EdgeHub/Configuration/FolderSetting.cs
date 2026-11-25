using System.Text.RegularExpressions;

namespace EdgeHub.Configuration;

/// <summary>
/// A folder named in the settings: it may name environment variables (<c>%USERPROFILE%</c>), a relative folder is
/// next to the service whatever folder it starts from, and an empty one turns the feature off.
/// </summary>
public static partial class FolderSetting
{
    /// <summary>The folder as a full path, or null when it is empty.</summary>
    public static string? Resolve(string? folder) =>
        string.IsNullOrWhiteSpace(folder)
            ? null
            : Path.GetFullPath(Environment.ExpandEnvironmentVariables(folder), AppContext.BaseDirectory);

    /// <summary>
    /// The first environment variable the folder names that does not exist, such as <c>%NO_SUCH%</c>, or null. Windows
    /// leaves an unknown variable as it is, so the folder would silently be named after it.
    /// </summary>
    public static string? UnknownVariable(string? folder)
    {
        if (string.IsNullOrWhiteSpace(folder))
        {
            return null;
        }

        Match unknown = Variable().Match(Environment.ExpandEnvironmentVariables(folder));
        return unknown.Success ? unknown.Value : null;
    }

    [GeneratedRegex(@"%[^%\\/]+%")]
    private static partial Regex Variable();
}
