namespace Qlipq.Host;

public static class PathUtil
{
    /// <summary>Normalize a path to forward slashes (matches the frontend's <c>toPosixPath</c>).</summary>
    public static string ToPosix(string path) => path.Replace('\\', '/');
}

/// <summary>Filesystem size + modified time for queue display (mirrors the Rust <c>file_info</c>).</summary>
public sealed record FileInfoResult
{
    public string Path { get; set; } = "";
    public long Size { get; set; }
    public long ModifiedMs { get; set; }
}

public static class FileOps
{
    public static List<FileInfoResult> FileInfoBatch(IEnumerable<string> paths)
    {
        var result = new List<FileInfoResult>();
        foreach (var path in paths)
        {
            FileInfo fi;
            try
            {
                fi = new FileInfo(path);
                if (!fi.Exists) continue;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException) { continue; }

            long modifiedMs;
            try { modifiedMs = new DateTimeOffset(fi.LastWriteTimeUtc).ToUnixTimeMilliseconds(); }
            catch (ArgumentOutOfRangeException) { modifiedMs = 0; }

            result.Add(new FileInfoResult { Path = path, Size = fi.Length, ModifiedMs = modifiedMs });
        }
        return result;
    }

    public static bool FileExists(string path) => File.Exists(path);

    /// <summary>Rename a file on disk; returns the new path. Cross-device moves fall back to copy+delete.</summary>
    public static string RenameFile(string from, string to)
    {
        if (string.Equals(from, to, StringComparison.Ordinal)) return to;
        if (File.Exists(to)) throw new IOException($"A file already exists at {to}");

        var parent = Path.GetDirectoryName(to);
        if (!string.IsNullOrEmpty(parent)) Directory.CreateDirectory(parent);

        try
        {
            File.Move(from, to);
        }
        catch (IOException)
        {
            // Cross-device move (e.g. to another drive): copy then remove the source.
            File.Copy(from, to);
            File.Delete(from);
        }
        return to;
    }

    public static void DeleteFile(string path) => File.Delete(path);
}

/// <summary>
/// Recursively collect video files in the given folders and all subfolders, skipping reparse
/// points (symlinks/junctions) so they are not followed — matching the Rust <c>scan_folders</c>.
/// </summary>
public static class Scanner
{
    public static List<string> ScanFolders(IEnumerable<string> folders, IReadOnlyList<string> extensions)
    {
        var found = new List<string>();
        var stack = new Stack<string>(folders);
        while (stack.Count > 0)
        {
            var dir = stack.Pop();
            IEnumerable<string> entries;
            try { entries = Directory.EnumerateFileSystemEntries(dir); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { continue; }

            foreach (var entry in entries)
            {
                FileAttributes attr;
                try { attr = File.GetAttributes(entry); }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException) { continue; }

                if ((attr & FileAttributes.ReparsePoint) != 0) continue;
                if ((attr & FileAttributes.Directory) != 0) stack.Push(entry);
                else if (HasVideoExt(entry, extensions)) found.Add(entry);
            }
        }
        return found;
    }

    private static bool HasVideoExt(string path, IReadOnlyList<string> extensions)
    {
        var ext = Path.GetExtension(path);
        if (ext.Length == 0) return false;
        ext = ext[1..];
        foreach (var x in extensions)
        {
            if (string.Equals(x, ext, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }
}
