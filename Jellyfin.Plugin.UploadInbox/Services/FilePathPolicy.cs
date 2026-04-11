using System;
using System.IO;
using System.Text;
using Jellyfin.Plugin.UploadInbox.Configuration;

namespace Jellyfin.Plugin.UploadInbox.Services;

/// <summary>
/// Responsible for mapping logical uploads to safe filesystem paths.
/// </summary>
public class FilePathPolicy
{
    private const int MaxCollisionAttempts = 1000;

    /// <summary>
    /// Creates filesystem paths for a new upload.
    /// </summary>
    /// <param name="target">Upload target configuration.</param>
    /// <param name="userId">User id.</param>
    /// <param name="originalFileName">Original file name from client.</param>
    /// <param name="uploadId">Upload id to incorporate into temp path.</param>
    /// <returns>Tuple of temp path, final path, and sanitized file name.</returns>
    public (string TempFilePath, string FinalFilePath, string SanitizedFileName) CreatePaths(
        UploadTarget target,
        Guid userId,
        string originalFileName,
        string uploadId)
    {
        var targetDirectory = ResolveTargetDirectory(target);
        Directory.CreateDirectory(targetDirectory);

        var sanitizedName = SanitizeFileName(originalFileName);
        if (string.IsNullOrWhiteSpace(sanitizedName))
        {
            throw new InvalidOperationException("File name is invalid.");
        }

        var extension = Path.GetExtension(sanitizedName);
        if (target.AllowedExtensions is not null && target.AllowedExtensions.Count > 0)
        {
            var extWithoutDot = extension.StartsWith(".", StringComparison.Ordinal) ? extension[1..] : extension;
            var allowed = target.AllowedExtensions.Exists(
                e => string.Equals(e, extWithoutDot, StringComparison.OrdinalIgnoreCase));
            if (!allowed)
            {
                throw new InvalidOperationException("File extension is not allowed for this target.");
            }
        }

        var finalPath = Path.Combine(targetDirectory, sanitizedName);
        finalPath = EnsureWithinBaseDirectory(targetDirectory, finalPath);

        // Collision policy: append (2), (3), ...
        var attempt = 1;
        var nameWithoutExt = Path.GetFileNameWithoutExtension(sanitizedName);
        while (File.Exists(finalPath))
        {
            attempt++;
            if (attempt > MaxCollisionAttempts)
            {
                throw new IOException("Too many files with the same name in target directory.");
            }

            var candidateName = new StringBuilder(nameWithoutExt)
                .Append(" (")
                .Append(attempt.ToString(System.Globalization.CultureInfo.InvariantCulture))
                .Append(')')
                .Append(extension)
                .ToString();

            finalPath = Path.Combine(targetDirectory, candidateName);
            finalPath = EnsureWithinBaseDirectory(targetDirectory, finalPath);
        }

        var tempFileName = sanitizedName + "." + uploadId + ".part";
        var tempPath = Path.Combine(targetDirectory, tempFileName);
        tempPath = EnsureWithinBaseDirectory(targetDirectory, tempPath);

        return (tempPath, finalPath, Path.GetFileName(finalPath));
    }

    /// <summary>
    /// Resolves the effective upload directory for a target.
    /// </summary>
    /// <param name="target">Upload target configuration.</param>
    /// <returns>Absolute path to the effective upload directory.</returns>
    public string ResolveTargetDirectory(UploadTarget target)
    {
        if (string.IsNullOrWhiteSpace(target.LibraryPath))
        {
            throw new InvalidOperationException("Target library folder is not configured.");
        }

        var basePathFull = Path.GetFullPath(target.LibraryPath);
        if (!Path.IsPathRooted(basePathFull))
        {
            throw new InvalidOperationException("Target library folder must be absolute.");
        }

        var subdirectory = NormalizeUploadSubdirectory(target.UploadSubdirectory);
        if (string.IsNullOrEmpty(subdirectory))
        {
            return basePathFull;
        }

        var resolved = Path.Combine(basePathFull, subdirectory);
        return EnsureWithinBaseDirectory(basePathFull, resolved);
    }

    private static string NormalizeUploadSubdirectory(string? uploadSubdirectory)
    {
        if (string.IsNullOrWhiteSpace(uploadSubdirectory))
        {
            return string.Empty;
        }

        var trimmed = uploadSubdirectory.Trim();
        if (Path.IsPathRooted(trimmed))
        {
            throw new InvalidOperationException("Upload subfolder must be a relative path inside the selected library.");
        }

        var separators = new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar, '/', '\\' };
        var segments = trimmed
            .Split(separators, StringSplitOptions.RemoveEmptyEntries);

        if (segments.Length == 0)
        {
            return string.Empty;
        }

        var invalidChars = Path.GetInvalidFileNameChars();
        var normalizedSegments = new string[segments.Length];
        for (var i = 0; i < segments.Length; i++)
        {
            var segment = segments[i].Trim();
            if (segment.Length == 0 || string.Equals(segment, ".", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Upload subfolder contains an invalid path segment.");
            }

            if (string.Equals(segment, "..", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Upload subfolder cannot traverse outside the selected library.");
            }

            if (segment.IndexOfAny(invalidChars) >= 0)
            {
                throw new InvalidOperationException("Upload subfolder contains invalid filesystem characters.");
            }

            normalizedSegments[i] = segment;
        }

        return Path.Combine(normalizedSegments);
    }

    private static string SanitizeFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return string.Empty;
        }

        var justName = Path.GetFileName(fileName);

        if (string.Equals(justName, ".", StringComparison.Ordinal) ||
            string.Equals(justName, "..", StringComparison.Ordinal))
        {
            return string.Empty;
        }

        var invalidChars = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(justName.Length);
        foreach (var ch in justName)
        {
            if (Array.IndexOf(invalidChars, ch) >= 0)
            {
                builder.Append('_');
            }
            else
            {
                builder.Append(ch);
            }
        }

        return builder.ToString();
    }

    private static string EnsureWithinBaseDirectory(string basePathFull, string path)
    {
        var full = Path.GetFullPath(path);
        var relative = Path.GetRelativePath(basePathFull, full);

        if (Path.IsPathRooted(relative) ||
            relative.StartsWith("..", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Resolved path escapes the configured base directory.");
        }

        return full;
    }
}
