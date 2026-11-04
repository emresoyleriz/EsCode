using System.Security.Cryptography;
using System.Text;

// G4.1 read-only workspace content access for trusted Host code.
// No generic IPC, write or agent access is introduced.
internal sealed record WorkspaceTextSnapshot(string RelativePath, string Content, string Sha256, long ByteLength);

internal sealed class WorkspaceReader
{
    private const int MaxTextSize = 1_048_576;
    private readonly string _root;
    private readonly string _rootPrefix;
    private readonly object _mutation = new();
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private static readonly HashSet<string> DeniedSegments = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git", ".svn", ".hg", "node_modules", "bin", "obj", ".vs", ".idea",
        ".env", ".ssh", ".aws", ".gnupg"
    };

    internal WorkspaceReader(string root)
    {
        if (!Path.IsPathFullyQualified(root) || !Directory.Exists(root))
            throw new ArgumentException("Workspace root must be an existing absolute directory");
        if (OperatingSystem.IsWindows() && root.StartsWith(@"\\", StringComparison.Ordinal))
            throw new UnauthorizedAccessException("Network-share roots are not trusted by default");
        _root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        for (var directory = new DirectoryInfo(_root); directory is not null; directory = directory.Parent)
        {
            if ((directory.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new UnauthorizedAccessException("Reparse-point root or ancestor forbidden");
        }
        _rootPrefix = _root.EndsWith(Path.DirectorySeparatorChar) ? _root : _root + Path.DirectorySeparatorChar;
    }

    private string Resolve(string relative, bool directory = false)
    {
        if (string.IsNullOrWhiteSpace(relative) || relative.Length > 1024 ||
            Path.IsPathRooted(relative) || relative.Contains('\\') ||
            relative.Contains(':') || relative.Contains('\0') || relative.Contains('%'))
            throw new UnauthorizedAccessException("Invalid workspace path");
        var segments = relative.Split('/');
        if (segments.Any(x => x.Length == 0 || x is "." or ".." || x.StartsWith(".env", StringComparison.OrdinalIgnoreCase)
            || DeniedSegments.Contains(x) || x.Length > 255))
            throw new UnauthorizedAccessException("Denied workspace path");
        var full = Path.GetFullPath(Path.Combine(_root, Path.Combine(segments)));
        var cmp = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (!full.StartsWith(_rootPrefix, cmp))
            throw new UnauthorizedAccessException("Path escapes workspace");
        var current = _root;
        foreach (var segment in segments)
        {
            current = Path.Combine(current, segment);
            var attrs = File.GetAttributes(current); // Reject missing paths and junctions.
            if ((attrs & FileAttributes.ReparsePoint) != 0)
                throw new UnauthorizedAccessException("Symlink/junction access denied");
        }
        if (directory ? !Directory.Exists(full) : !File.Exists(full))
            throw new FileNotFoundException("Expected workspace entry unavailable");
        return full;
    }

    internal WorkspaceTextSnapshot ReadText(string relative)
    {
        var full = Resolve(relative);
        using var stream = new FileStream(full, FileMode.Open, FileAccess.Read, FileShare.Read, 8192,
            FileOptions.SequentialScan);
        if (stream.Length > MaxTextSize) throw new InvalidDataException("File exceeds 1 MiB text limit");
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        if (buffer.Length > MaxTextSize) throw new InvalidDataException("File changed while reading");
        var bytes = buffer.ToArray();
        if (bytes.Contains((byte)0)) throw new InvalidDataException("Binary files cannot be read as text");
        string content;
        try { content = StrictUtf8.GetString(bytes); }
        catch (DecoderFallbackException) { throw new InvalidDataException("Non-UTF8 file"); }
        return new WorkspaceTextSnapshot(relative, content,
            "sha256:" + Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(), bytes.Length);
    }

    // Trusted internal patch only. This cannot substitute for an editor-buffer revision grant:
    // external mutations between final hash check and rename still require a platform handle gate.
    internal WorkspaceTextSnapshot ReplaceText(string relative, string expectedDigest, string newContent)
    {
        if (expectedDigest is null || expectedDigest.Length != 71 ||
            !expectedDigest.StartsWith("sha256:", StringComparison.Ordinal) ||
            expectedDigest.AsSpan(7).IndexOfAnyExcept("0123456789abcdef") >= 0)
            throw new ArgumentException("Invalid expected digest");
        if (newContent is null || newContent.Contains('\0'))
            throw new ArgumentException("Invalid UTF-8 text content");
        var bytes = StrictUtf8.GetBytes(newContent);
        if (bytes.Length > MaxTextSize) throw new InvalidDataException("Write exceeds 1 MiB text limit");

        lock (_mutation)
        {
            var full = Resolve(relative);
            var old = ReadText(relative);
            if (!string.Equals(old.Sha256, expectedDigest, StringComparison.Ordinal))
                throw new InvalidOperationException("Stale file snapshot; refusing overwrite");
            var temporary = Path.Combine(Path.GetDirectoryName(full)!,
                ".escode-patch-" + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                using (var staged = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write,
                    FileShare.None, 8192, FileOptions.WriteThrough))
                {
                    staged.Write(bytes);
                    staged.Flush(flushToDisk: true);
                }
                // Revalidate path and digest before the atomic same-directory replacement.
                if (Resolve(relative) != full || ReadText(relative).Sha256 != expectedDigest)
                    throw new InvalidOperationException("Workspace file changed before replace");
                File.Replace(temporary, full, null);
                return ReadText(relative);
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        }
    }

    internal IReadOnlyList<string> ListTextCandidates(int maxEntries = 128, int maxDepth = 6)
    {
        if (maxEntries is < 1 or > 1024 || maxDepth is < 1 or > 20)
            throw new ArgumentOutOfRangeException(nameof(maxEntries));
        var output = new List<string>();
        var pending = new Stack<(string Relative, int Depth)>();
        // Do not traverse beyond the configured depth or follow any reparse point.
        pending.Push(("", 0));
        while (pending.Count > 0 && output.Count < maxEntries)
        {
            var (relative, depth) = pending.Pop();
            var directory = relative.Length == 0 ? _root : Resolve(relative, true);
            foreach (var full in Directory.EnumerateFileSystemEntries(directory).OrderBy(x => x, StringComparer.Ordinal))
            {
                var name = Path.GetFileName(full);
                if (DeniedSegments.Contains(name) || name.StartsWith(".env", StringComparison.OrdinalIgnoreCase))
                    continue;
                var attrs = File.GetAttributes(full);
                if ((attrs & FileAttributes.ReparsePoint) != 0) continue;
                var child = relative.Length == 0 ? name : relative + "/" + name;
                if ((attrs & FileAttributes.Directory) != 0)
                {
                    if (depth + 1 < maxDepth) pending.Push((child, depth + 1));
                }
                else if (output.Count < maxEntries && (attrs & FileAttributes.Directory) == 0)
                {
                    output.Add(child);
                }
            }
        }
        output.Sort(StringComparer.Ordinal);
        return output.AsReadOnly();
    }
}

internal static class WorkspaceReadSpike
{
    internal static bool Run()
    {
        var root = Path.Combine(Path.GetTempPath(), "escode-workspace-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "src"));
        Directory.CreateDirectory(Path.Combine(root, ".git"));
        try
        {
            File.WriteAllText(Path.Combine(root, "src", "hello.cs"), "namespace Test; class Hello {}");
            File.WriteAllText(Path.Combine(root, ".env"), "SECRET=do-not-read");
            File.WriteAllText(Path.Combine(root, ".git", "config"), "secret");
            File.WriteAllBytes(Path.Combine(root, "src", "binary.bin"), new byte[] { 0, 1, 2 });
            var reader = new WorkspaceReader(root);
            var snapshot = reader.ReadText("src/hello.cs");
            if (snapshot.Content != "namespace Test; class Hello {}" ||
                snapshot.Sha256.Length != 71 || snapshot.ByteLength == 0) return false;
            var updated = reader.ReplaceText("src/hello.cs", snapshot.Sha256, "namespace Test; class Updated {}");
            if (updated.Content != "namespace Test; class Updated {}" || updated.Sha256 == snapshot.Sha256)
                return false;
            try { reader.ReplaceText("src/hello.cs", snapshot.Sha256, "unsafe overwrite"); return false; }
            catch (InvalidOperationException) { }
            var list = reader.ListTextCandidates();
            if (!list.Contains("src/hello.cs") || list.Any(x => x.StartsWith(".git") || x.StartsWith(".env"))) return false;
            foreach (var invalid in new[] { "../outside", "src/../../outside", ".git/config", ".env",
                "src\\hello.cs", "/etc/passwd", "C:/Windows/win.ini", "src/%2e%2e/x" })
            {
                try { reader.ReadText(invalid); return false; }
                catch (Exception ex) when (ex is UnauthorizedAccessException or ArgumentException or FileNotFoundException) { }
            }
            try { reader.ReadText("src/binary.bin"); return false; }
            catch (InvalidDataException) { }
            return true;
        }
        finally { Directory.Delete(root, true); }
    }
}
