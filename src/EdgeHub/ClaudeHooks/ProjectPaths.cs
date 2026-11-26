namespace EdgeHub.ClaudeHooks;

/// <summary>Paths seen from the project's folder, the <c>cwd</c> of a hook, as Windows reads them (case ignored).</summary>
internal static class ProjectPaths
{
    /// <summary>The full path of <paramref name="path"/> read from <paramref name="project"/>, or null when it is not a path.</summary>
    public static string? Full(string path, string project)
    {
        try
        {
            return Path.GetFullPath(path, Path.GetFullPath(project));
        }
        catch (Exception e) when (e is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }
    }

    /// <summary>Whether a full path is the project's folder or inside it: <c>bookshelf-old</c> is not in <c>bookshelf</c>.</summary>
    public static bool IsInside(string fullPath, string project)
    {
        if (Full(project, project) is not { } projectPath)
        {
            return false;
        }

        string root = Path.TrimEndingDirectorySeparator(projectPath);
        string path = Path.TrimEndingDirectorySeparator(fullPath);
        return path.Equals(root, StringComparison.OrdinalIgnoreCase)
            || path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The last folder name of the project, never the full path.</summary>
    public static string? FolderName(string? cwd) =>
        string.IsNullOrWhiteSpace(cwd) ? null : Path.GetFileName(cwd.TrimEnd('\\', '/')) is { Length: > 0 } name ? name : null;
}
