using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

// G4.2: a *trusted-host-only* worktree primitive. Not exposed over IPC or to agents.
// Git hooks/filters remain an execution threat for untrusted repos: production use BLOCKED.
internal static class GitWorktreeService
{
    private static readonly Regex StoryName = new("^[a-z0-9][a-z0-9-]{0,49}$", RegexOptions.CultureInvariant);
    internal static string ApprovalDigest(string repo, string destination, string story)
    {
        var canonical = Path.GetFullPath(repo) + "\n" + Path.GetFullPath(destination) + "\n" + story;
        return "sha256:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
    }

    internal static async Task<string> CreateTrustedStoryWorktreeAsync(
        string gitExe, string repo, string destinationRoot, string story,
        ToolPolicyBroker broker, string? token, string workspaceId)
    {
        if (!StoryName.IsMatch(story) || !Path.IsPathFullyQualified(gitExe) ||
            !File.Exists(gitExe) || !Path.IsPathFullyQualified(repo) ||
            !Directory.Exists(Path.Combine(repo, ".git")) ||
            !Path.IsPathFullyQualified(destinationRoot) || !Directory.Exists(destinationRoot))
            throw new UnauthorizedAccessException("Git worktree prerequisites not satisfied");
        var target = Path.Combine(Path.GetFullPath(destinationRoot), story);
        if (Directory.Exists(target) || File.Exists(target))
            throw new IOException("Worktree destination already exists");
        var intent = new BrokerIntent("trusted-user", "git.worktree", "workspace/repo",
            ApprovalDigest(repo, target, story), ToolRisk.Low);
        if (!broker.Consume(token, intent, workspaceId).Allowed)
            throw new UnauthorizedAccessException("Missing one-time Git worktree approval");
        // Only this exact approved command is executed. No arbitrary arguments/shell.
        var result = await GitCommandAsync(gitExe, repo, new[]
        {
            "-c", "core.hooksPath=NUL", "worktree", "add", "-b",
            "story/" + story, target, "refs/heads/main"
        });
        if (result.ExitCode != 0)
            throw new InvalidOperationException("Git worktree creation failed (details suppressed)");
        return target;
    }

    internal static async Task<(int ExitCode, string Output)> GitCommandAsync(
        string gitExe, string workDir, IEnumerable<string> arguments, int timeoutSeconds = 20)
    {
        if (!Path.IsPathFullyQualified(gitExe) || !File.Exists(gitExe) ||
            !Path.IsPathFullyQualified(workDir) || !Directory.Exists(workDir))
            throw new UnauthorizedAccessException("Invalid Git executable or working directory");
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo(gitExe)
        {
            WorkingDirectory = workDir,
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true
        };
        process.StartInfo.Environment["GIT_TERMINAL_PROMPT"] = "0";
        process.StartInfo.Environment["GCM_INTERACTIVE"] = "Never";
        foreach (var argument in arguments) process.StartInfo.ArgumentList.Add(argument);
        if (!process.Start()) throw new InvalidOperationException("Git failed to start");
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
        try
        {
            var stdout = process.StandardOutput.ReadToEndAsync(deadline.Token);
            var stderr = process.StandardError.ReadToEndAsync(deadline.Token);
            await process.WaitForExitAsync(deadline.Token);
            var output = await stdout;
            _ = await stderr; // Never expose potentially sensitive Git diagnostics.
            if (output.Length > 131_072) throw new InvalidDataException("Git output cap exceeded");
            return (process.ExitCode, output);
        }
        catch
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            throw;
        }
    }
}

internal static class GitWorktreeSpike
{
    private static async Task<string> LocateGit()
    {
        // The test fixture resolves Git from CI; production must pin a trusted executable.
        using var finder = Process.Start(new ProcessStartInfo("where.exe", "git.exe")
        {
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true
        }) ?? throw new InvalidOperationException("Git locator failed");
        var text = await finder.StandardOutput.ReadToEndAsync();
        await finder.WaitForExitAsync();
        var git = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        if (finder.ExitCode != 0 || git is null || !File.Exists(git))
            throw new FileNotFoundException("Git unavailable on Windows test runner");
        return git;
    }

    internal static async Task<bool> RunAsync()
    {
        if (!OperatingSystem.IsWindows()) return false;
        var root = Path.Combine(Path.GetTempPath(), "escode-git-" + Guid.NewGuid().ToString("N"));
        var repo = Path.Combine(root, "repo");
        var worktrees = Path.Combine(root, "worktrees");
        Directory.CreateDirectory(repo); Directory.CreateDirectory(worktrees);
        try
        {
            var git = await LocateGit();
            if ((await GitWorktreeService.GitCommandAsync(git, repo, new[] { "init", "-b", "main" })).ExitCode != 0)
                return false;
            await File.WriteAllTextAsync(Path.Combine(repo, "README.md"), "fixture\n");
            if ((await GitWorktreeService.GitCommandAsync(git, repo, new[] { "add", "README.md" })).ExitCode != 0)
                return false;
            if ((await GitWorktreeService.GitCommandAsync(git, repo, new[]
            { "-c", "user.name=EsCode Test", "-c", "user.email=escode@example.invalid", "commit", "-m", "fixture" })).ExitCode != 0)
                return false;
            var broker = new ToolPolicyBroker(BrokerMode.Supervised, "workspace-A");
            var destination = Path.Combine(worktrees, "story1");
            var intent = new BrokerIntent("trusted-user", "git.worktree", "workspace/repo",
                GitWorktreeService.ApprovalDigest(repo, destination, "story1"), ToolRisk.Low);
            var grant = broker.GrantAfterTrustedHumanApproval(intent, "workspace-A", TimeSpan.FromMinutes(1));
            if (grant is null) return false;
            try
            {
                await GitWorktreeService.CreateTrustedStoryWorktreeAsync(
                    git, repo, worktrees, "story1", broker, "bad-token", "workspace-A");
                return false;
            }
            catch (UnauthorizedAccessException) { }
            var created = await GitWorktreeService.CreateTrustedStoryWorktreeAsync(
                git, repo, worktrees, "story1", broker, grant, "workspace-A");
            if (!File.Exists(Path.Combine(created, "README.md"))) return false;
            try
            {
                await GitWorktreeService.CreateTrustedStoryWorktreeAsync(
                    git, repo, worktrees, "story2", broker, grant, "workspace-A");
                return false;
            }
            catch (UnauthorizedAccessException) { }
            var branch = await GitWorktreeService.GitCommandAsync(git, created,
                new[] { "branch", "--show-current" });
            return branch.ExitCode == 0 && branch.Output.Trim() == "story/story1";
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
