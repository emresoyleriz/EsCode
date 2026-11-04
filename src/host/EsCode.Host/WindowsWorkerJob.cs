using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

// Windows Job Objects are a *containment primitive*, NOT a complete security sandbox.
// Never run untrusted tools based solely on this type. The actual tool path remains disabled.
internal sealed class WindowsWorkerJob : IDisposable
{
    private const uint JobObjectLimitActiveProcess = 0x00000008;
    private const uint JobObjectLimitJobMemory = 0x00000200;
    private const uint JobObjectLimitKillOnJobClose = 0x00002000;
    private const int JobObjectExtendedLimitInformation = 9;
    private IntPtr _job;

    internal WindowsWorkerJob(int maxProcesses = 1, long maxMemoryBytes = 256L * 1024 * 1024)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Windows-only Job Object");
        if (maxProcesses is < 1 or > 16 || maxMemoryBytes < 64L * 1024 * 1024)
            throw new ArgumentOutOfRangeException(nameof(maxProcesses), "Unsafe worker limits");
        _job = CreateJobObject(IntPtr.Zero, null);
        if (_job == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
        try
        {
            var limits = new JobObjectExtendedLimitInformationNative
            {
                BasicLimitInformation =
                {
                    LimitFlags = JobObjectLimitActiveProcess | JobObjectLimitJobMemory | JobObjectLimitKillOnJobClose,
                    ActiveProcessLimit = (uint)maxProcesses
                },
                JobMemoryLimit = (UIntPtr)(ulong)maxMemoryBytes
            };
            if (!SetInformationJobObject(_job, JobObjectExtendedLimitInformation,
                    ref limits, (uint)Marshal.SizeOf<JobObjectExtendedLimitInformationNative>()))
                throw new Win32Exception(Marshal.GetLastWin32Error());
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    internal void Attach(Process worker)
    {
        if (_job == IntPtr.Zero) throw new ObjectDisposedException(nameof(WindowsWorkerJob));
        if (worker.HasExited) throw new InvalidOperationException("Worker exited before containment");
        if (!AssignProcessToJobObject(_job, worker.Handle))
            throw new Win32Exception(Marshal.GetLastWin32Error());
    }

    public void Dispose()
    {
        var value = Interlocked.Exchange(ref _job, IntPtr.Zero);
        if (value != IntPtr.Zero) CloseHandle(value);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JobObjectBasicLimitInformationNative
    {
        internal long PerProcessUserTimeLimit;
        internal long PerJobUserTimeLimit;
        internal uint LimitFlags;
        internal UIntPtr MinimumWorkingSetSize;
        internal UIntPtr MaximumWorkingSetSize;
        internal uint ActiveProcessLimit;
        internal UIntPtr Affinity;
        internal uint PriorityClass;
        internal uint SchedulingClass;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct IoCountersNative
    {
        internal ulong ReadOperationCount;
        internal ulong WriteOperationCount;
        internal ulong OtherOperationCount;
        internal ulong ReadTransferCount;
        internal ulong WriteTransferCount;
        internal ulong OtherTransferCount;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct JobObjectExtendedLimitInformationNative
    {
        internal JobObjectBasicLimitInformationNative BasicLimitInformation;
        internal IoCountersNative IoInfo;
        internal UIntPtr ProcessMemoryLimit;
        internal UIntPtr JobMemoryLimit;
        internal UIntPtr PeakProcessMemoryUsed;
        internal UIntPtr PeakJobMemoryUsed;
    }

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateJobObject(IntPtr jobAttributes, string? name);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetInformationJobObject(IntPtr job, int informationClass,
        ref JobObjectExtendedLimitInformationNative information, uint length);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AssignProcessToJobObject(IntPtr job, IntPtr process);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);
}

internal static class WorkerContainmentSpike
{
    // Deliberately only spawns this assembly's harmless probe. No user-defined command is accepted.
    internal static async Task<bool> RunAsync()
    {
        if (!OperatingSystem.IsWindows()) return false;
        var assembly = typeof(WorkerContainmentSpike).Assembly.Location;
        using var worker = new Process
        {
            StartInfo = new ProcessStartInfo("dotnet")
            {
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };
        worker.StartInfo.ArgumentList.Add(assembly);
        worker.StartInfo.ArgumentList.Add("--worker-containment-child");
        worker.Start();
        try
        {
            using (var job = new WindowsWorkerJob(maxProcesses: 1))
            {
                job.Attach(worker);
                if (worker.HasExited) return false;
            } // Kill-on-job-close must terminate the worker.
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(8));
            try { await worker.WaitForExitAsync(timeout.Token); }
            catch (OperationCanceledException) { return false; }
            return worker.HasExited;
        }
        finally
        {
            if (!worker.HasExited) worker.Kill(entireProcessTree: true);
        }
    }
}
