using System;
using System.IO;
using Jellyfin.Plugin.UploadInbox.Configuration;
using Jellyfin.Plugin.UploadInbox.Services;
using Xunit;

namespace Jellyfin.Plugin.UploadInbox.Tests;

public class FilePathPolicyTests
{
    [Fact]
    public void CreatePaths_EnforcesExtensionAllowlist()
    {
        var target = new UploadTarget
        {
            Id = "t1",
            LibraryPath = Path.GetTempPath(),
            MaxFileSizeBytes = long.MaxValue,
        };
        target.AllowedExtensions.Add("mkv");

        var policy = new FilePathPolicy();
        var userId = Guid.NewGuid();

        // Allowed
        var result = policy.CreatePaths(target, userId, "movie.mkv", "upload1");
        Assert.EndsWith(".mkv", result.FinalFilePath, StringComparison.OrdinalIgnoreCase);

        // Disallowed
        Assert.Throws<InvalidOperationException>(() =>
            policy.CreatePaths(target, userId, "song.mp3", "upload2"));
    }

    [Fact]
    public void CreatePaths_PreventsTraversalOutsideBase()
    {
        var baseDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(baseDir);

        var target = new UploadTarget
        {
            Id = "t2",
            LibraryPath = baseDir,
            MaxFileSizeBytes = long.MaxValue,
        };

        var policy = new FilePathPolicy();
        var userId = Guid.NewGuid();

        // The policy rejects traversal attempts rather than silently sanitizing them.
        Assert.Throws<InvalidOperationException>(() =>
            policy.CreatePaths(target, userId, "..\\evil.mkv", "upload3"));
    }

    [Fact]
    public void CreatePaths_AutoRenamesOnCollision()
    {
        var baseDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(baseDir);

        var existingPath = Path.Combine(baseDir, "file.mkv");
        File.WriteAllText(existingPath, "existing");

        var target = new UploadTarget
        {
            Id = "t3",
            LibraryPath = baseDir,
            MaxFileSizeBytes = long.MaxValue,
        };

        var policy = new FilePathPolicy();
        var userId = Guid.NewGuid();

        var result = policy.CreatePaths(target, userId, "file.mkv", "upload4");
        Assert.NotEqual(existingPath, result.FinalFilePath);
        Assert.Contains("file (", Path.GetFileNameWithoutExtension(result.FinalFilePath), StringComparison.OrdinalIgnoreCase);
    }
}

