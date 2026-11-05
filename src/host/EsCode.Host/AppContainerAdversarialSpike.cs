using System.Net;
using System.Net.Sockets;

// Test-only AppContainer adversarial controls, not an agent worker.
internal static class AppContainerAdversarialSpike
{
    internal static async Task<bool> RunAsync()
    {
        if (!OperatingSystem.IsWindows()) return false;
        var root = Path.Combine(Path.GetTempPath(),
            "escode-sandbox-negative-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            // Denied behavior only counts if the same binaries can start.
            if (!await AppContainerProcessSpike.RunProbeAsync(
                "cmd.exe", "/D /C exit /B 0")) return false;
            if (!await AppContainerProcessSpike.RunProbeAsync(
                "curl.exe", "--version")) return false;

            var privateFile = Path.Combine(root, "outside-workspace.txt");
            var contents = "probe-" + Guid.NewGuid().ToString("N");
            await File.WriteAllTextAsync(privateFile, contents);
            if (await File.ReadAllTextAsync(privateFile) != contents) return false;
            if (await AppContainerProcessSpike.RunProbeAsync(
                "cmd.exe", "/D /C type \"" + privateFile + "\"")) return false;

            // Active process limit: a contained cmd must not spawn a second exe.
            if (await AppContainerProcessSpike.RunProbeAsync(
                "cmd.exe", "/D /C whoami.exe /user")) return false;

            using var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(8));
            var accept = listener.AcceptTcpClientAsync(deadline.Token);
            var result = await AppContainerProcessSpike.RunProbeAsync(
                "curl.exe", "--noproxy * --max-time 3 http://127.0.0.1:" + port + "/");
            if (result || accept.IsCompletedSuccessfully)
            {
                if (accept.IsCompletedSuccessfully) accept.Result.Dispose();
                return false;
            }
            await Task.Delay(250);
            if (accept.IsCompletedSuccessfully)
            {
                accept.Result.Dispose();
                return false;
            }
            deadline.Cancel();
            try { using var ignored = await accept; }
            catch (OperationCanceledException) { }
            return true;
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
