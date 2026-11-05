using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

// G2.1: PROBE ONLY. Windows CreateProcess(CREATE_SUSPENDED) prevents running
// child instructions before assigning a Job Object. This does NOT restrict
// filesystem, network, token privileges, handles, plugins or untrusted code.
internal static class SuspendedWorkerProbe
{
    private const uint CreateSuspended = 0x00000004;
    private const uint CreateNoWindow = 0x08000000;

    internal static async Task<bool> RunAsync()
    {
        if (!OperatingSystem.IsWindows()) return false;
        var executable = Environment.ProcessPath;
        if (string.IsNullOrEmpty(executable) || !Path.IsPathFullyQualified(executable) ||
            !File.Exists(executable)) return false;

        var assembly = typeof(SuspendedWorkerProbe).Assembly.Location;
        // The ONLY permitted test target is this compiled EsCode.Host binary.
        // Never accept renderer-, model-, Git-, or user-chosen executable paths here.
        var isDotnet = Path.GetFileName(executable).Equals("dotnet.exe", StringComparison.OrdinalIgnoreCase);
        var command = new StringBuilder(isDotnet
            ? $"\"{executable}\" \"{assembly}\" --worker-containment-child"
            : $"\"{executable}\" --worker-containment-child");

        var start = new StartupInfo { cb = (uint)Marshal.SizeOf<StartupInfo>() };
        if (!CreateProcess(executable, command, IntPtr.Zero, IntPtr.Zero, false,
                CreateSuspended | CreateNoWindow, IntPtr.Zero, null, ref start, out var child))
            throw new Win32Exception(Marshal.GetLastWin32Error());

        try
        {
            using var process = Process.GetProcessById(checked((int)child.ProcessId));
            using (var job = new WindowsWorkerJob(maxProcesses: 1))
            {
                // The primary thread is suspended until successful assignment.
                // Any failure falls through to TerminateProcess in finally.
                job.Attach(process);
                if (ResumeThread(child.ThreadHandle) == uint.MaxValue)
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                if (process.HasExited) return false;
            } // Job kill-on-close terminates the harmless probe.
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(8));
            try { await process.WaitForExitAsync(timeout.Token); }
            catch (OperationCanceledException) { return false; }
            return process.HasExited;
        }
        finally
        {
            // Terminate on any failure, including failed job assignment or thread resume.
            TerminateProcess(child.ProcessHandle, 1);
            CloseHandle(child.ThreadHandle);
            CloseHandle(child.ProcessHandle);
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct StartupInfo
    {
        public uint cb;
        public IntPtr reserved;
        public IntPtr desktop;
        public IntPtr title;
        public uint x, y, xSize, ySize, xCountChars, yCountChars, fillAttribute, flags;
        public ushort showWindow, reserved2;
        public IntPtr reservedBuffer;
        public IntPtr standardInput, standardOutput, standardError;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessInformation
    {
        public IntPtr ProcessHandle;
        public IntPtr ThreadHandle;
        public uint ProcessId;
        public uint ThreadId;
    }

    [DllImport("kernel32.dll", EntryPoint = "CreateProcessW", ExactSpelling = true,
        CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateProcess(
        string applicationName, StringBuilder commandLine,
        IntPtr processAttributes, IntPtr threadAttributes,
        [MarshalAs(UnmanagedType.Bool)] bool inheritHandles,
        uint creationFlags, IntPtr environment, string? currentDirectory,
        ref StartupInfo startup, out ProcessInformation information);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint ResumeThread(IntPtr thread);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool TerminateProcess(IntPtr process, uint exitCode);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);
}
