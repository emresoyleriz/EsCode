using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

// G2.1 Windows test only: launch ONE trusted OS utility with a real AppContainer
// token, verify the child token and zero capabilities BEFORE the first instruction,
// and assign a kill-on-close Job Object while the primary thread is suspended.
// This is not a general-purpose sandbox worker launcher or tool endpoint.
internal static class AppContainerProcessSpike
{
    private const uint CreateSuspended = 0x00000004;
    private const uint CreateNoWindow = 0x08000000;
    private const uint ExtendedStartupInfoPresent = 0x00080000;
    private const uint TokenQuery = 0x0008;
    private const int TokenIsAppContainer = 29;
    private const int TokenCapabilities = 30;
    private const int TokenAppContainerSid = 31;
    private static readonly IntPtr SecurityCapabilitiesAttribute = (IntPtr)0x00020009;

    internal static Task<bool> RunAsync() => RunProbeAsync("whoami.exe", "/user");

    // Fixture-specific Windows isolation probe; never exposed to renderer, tools or plugins.
    internal static async Task<bool> RunProbeAsync(string utility, string arguments)
    {
        if (!OperatingSystem.IsWindows()) return false;
        if (utility is not ("whoami.exe" or "cmd.exe" or "curl.exe") ||
            string.IsNullOrWhiteSpace(arguments) || arguments.Length > 512 ||
            arguments.Contains('\n') || arguments.Contains('\r'))
            throw new ArgumentException("Unapproved sandbox test fixture");
        var executable = Path.Combine(Environment.SystemDirectory, utility);
        if (!File.Exists(executable)) throw new FileNotFoundException("Known Windows test utility unavailable");
        var name = "EsCode.LaunchProbe." + Guid.NewGuid().ToString("N");
        IntPtr sid = IntPtr.Zero;
        IntPtr attributes = IntPtr.Zero;
        IntPtr securityData = IntPtr.Zero;
        IntPtr token = IntPtr.Zero;
        ProcessInformation child = default;
        bool hasProfile = false;
        bool attributesInitialized = false;

        try
        {
            var createHr = CreateAppContainerProfile(
                name, "EsCode isolated test", "Disposable zero-capability process test",
                IntPtr.Zero, 0, out sid);
            if (createHr != 0 || sid == IntPtr.Zero)
                throw new Win32Exception($"AppContainer profile unavailable (HRESULT 0x{createHr:X8})");
            hasProfile = true;

            IntPtr bytes = IntPtr.Zero;
            _ = InitializeProcThreadAttributeList(IntPtr.Zero, 1, 0, ref bytes);
            if (bytes.ToInt64() is <= 0 or > 65536)
                throw new InvalidOperationException("Invalid Windows process attribute allocation");
            attributes = Marshal.AllocHGlobal(bytes);
            if (!InitializeProcThreadAttributeList(attributes, 1, 0, ref bytes))
                throw new Win32Exception(Marshal.GetLastWin32Error());
            attributesInitialized = true;

            var capabilities = new SecurityCapabilities
            {
                AppContainerSid = sid,
                Capabilities = IntPtr.Zero,
                CapabilityCount = 0,
                Reserved = 0
            };
            securityData = Marshal.AllocHGlobal(Marshal.SizeOf<SecurityCapabilities>());
            Marshal.StructureToPtr(capabilities, securityData, false);
            if (!UpdateProcThreadAttribute(attributes, 0, SecurityCapabilitiesAttribute,
                    securityData, (IntPtr)Marshal.SizeOf<SecurityCapabilities>(),
                    IntPtr.Zero, IntPtr.Zero))
                throw new Win32Exception(Marshal.GetLastWin32Error());

            var startup = new StartupInfoEx
            {
                StartupInfo = new StartupInfo { cb = (uint)Marshal.SizeOf<StartupInfoEx>() },
                AttributeList = attributes
            };
            var argv = new StringBuilder("\"" + executable + "\" " + arguments);
            if (!CreateProcess(executable, argv, IntPtr.Zero, IntPtr.Zero, false,
                    CreateSuspended | CreateNoWindow | ExtendedStartupInfoPresent,
                    IntPtr.Zero, null, ref startup, out child))
                throw new Win32Exception(Marshal.GetLastWin32Error());

            // A child must NOT resume if any process-token check fails.
            if (!OpenProcessToken(child.ProcessHandle, TokenQuery, out token))
                throw new Win32Exception(Marshal.GetLastWin32Error());
            if (!GetTokenInformationInt(token, TokenIsAppContainer, out var isContainer,
                    sizeof(int), out _) || isContainer != 1)
                throw new InvalidOperationException("Child does not have an AppContainer token");
            if (!GetTokenInformationRaw(token, TokenAppContainerSid, IntPtr.Zero,
                    0, out var sidBufferLength) &&
                sidBufferLength is <= 0 or > 4096)
                throw new InvalidOperationException("Cannot inspect child container SID");
            if (sidBufferLength is < 4 or > 4096)
                throw new InvalidOperationException("Child SID buffer length is invalid");
            var sidBuffer = Marshal.AllocHGlobal(sidBufferLength);
            try
            {
                if (!GetTokenInformationRaw(token, TokenAppContainerSid, sidBuffer,
                        sidBufferLength, out _))
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                var actualSid = Marshal.ReadIntPtr(sidBuffer);
                if (actualSid == IntPtr.Zero || !EqualSid(sid, actualSid))
                    throw new InvalidOperationException("Child AppContainer SID mismatch");
            }
            finally { Marshal.FreeHGlobal(sidBuffer); }

            // TokenCapabilities must contain ZERO capability SIDs.
            _ = GetTokenInformationRaw(token, TokenCapabilities, IntPtr.Zero,
                0, out var capabilitiesLength);
            if (capabilitiesLength is < 4 or > 4096)
                throw new InvalidOperationException("Invalid capabilities token structure");
            var capabilityBuffer = Marshal.AllocHGlobal(capabilitiesLength);
            try
            {
                if (!GetTokenInformationRaw(token, TokenCapabilities, capabilityBuffer,
                        capabilitiesLength, out _))
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                if (Marshal.ReadInt32(capabilityBuffer) != 0)
                    throw new InvalidOperationException("Child unexpectedly has capabilities");
            }
            finally { Marshal.FreeHGlobal(capabilityBuffer); }

            using var process = Process.GetProcessById(checked((int)child.ProcessId));
            using (var job = new WindowsWorkerJob(maxProcesses: 1))
            {
                job.Attach(process); // suspended process; kill when job closes
                if (ResumeThread(child.ThreadHandle) == uint.MaxValue)
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                try { await process.WaitForExitAsync(deadline.Token); }
                catch (OperationCanceledException) { return false; }
                return process.ExitCode == 0;
            }
        }
        finally
        {
            if (child.ProcessHandle != IntPtr.Zero)
            {
                // Fail closed; no suspended child survives a failure.
                TerminateProcess(child.ProcessHandle, 1);
                WaitForSingleObject(child.ProcessHandle, 5000);
                CloseHandle(child.ProcessHandle);
            }
            if (child.ThreadHandle != IntPtr.Zero) CloseHandle(child.ThreadHandle);
            if (token != IntPtr.Zero) CloseHandle(token);
            if (attributesInitialized) DeleteProcThreadAttributeList(attributes);
            if (attributes != IntPtr.Zero) Marshal.FreeHGlobal(attributes);
            if (securityData != IntPtr.Zero) Marshal.FreeHGlobal(securityData);
            if (sid != IntPtr.Zero) FreeSid(sid);
            if (hasProfile)
            {
                var result = DeleteAppContainerProfile(name);
                if (result != 0)
                    throw new Win32Exception($"AppContainer profile cleanup failed (HRESULT 0x{result:X8})");
            }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SecurityCapabilities
    {
        internal IntPtr AppContainerSid;
        internal IntPtr Capabilities;
        internal uint CapabilityCount;
        internal uint Reserved;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct StartupInfo
    {
        internal uint cb;
        internal IntPtr reserved, desktop, title;
        internal uint x, y, xSize, ySize, xCountChars, yCountChars, fillAttribute, flags;
        internal ushort showWindow, reserved2;
        internal IntPtr reservedBuffer, standardInput, standardOutput, standardError;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct StartupInfoEx
    {
        internal StartupInfo StartupInfo;
        internal IntPtr AttributeList;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessInformation
    {
        internal IntPtr ProcessHandle;
        internal IntPtr ThreadHandle;
        internal uint ProcessId;
        internal uint ThreadId;
    }

    [DllImport("userenv.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int CreateAppContainerProfile(string name, string displayName,
        string description, IntPtr capabilities, uint capabilityCount, out IntPtr sid);

    [DllImport("userenv.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int DeleteAppContainerProfile(string name);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool InitializeProcThreadAttributeList(
        IntPtr list, int attributeCount, int flags, ref IntPtr size);

    [DllImport("kernel32.dll")]
    private static extern void DeleteProcThreadAttributeList(IntPtr list);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UpdateProcThreadAttribute(IntPtr list, uint flags,
        IntPtr attribute, IntPtr value, IntPtr size, IntPtr previousValue, IntPtr returnSize);

    [DllImport("kernel32.dll", EntryPoint = "CreateProcessW", ExactSpelling = true,
        CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateProcess(string applicationName, StringBuilder commandLine,
        IntPtr processAttributes, IntPtr threadAttributes,
        [MarshalAs(UnmanagedType.Bool)] bool inheritHandles, uint creationFlags,
        IntPtr environment, string? currentDirectory,
        ref StartupInfoEx startup, out ProcessInformation child);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool OpenProcessToken(IntPtr processHandle, uint desiredAccess,
        out IntPtr tokenHandle);

    [DllImport("advapi32.dll", EntryPoint = "GetTokenInformation", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetTokenInformationInt(IntPtr token, int kind,
        out int information, int capacity, out int returned);

    [DllImport("advapi32.dll", EntryPoint = "GetTokenInformation", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetTokenInformationRaw(IntPtr token, int kind,
        IntPtr buffer, int capacity, out int returned);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EqualSid(IntPtr left, IntPtr right);

    [DllImport("advapi32.dll")]
    private static extern IntPtr FreeSid(IntPtr sid);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint ResumeThread(IntPtr thread);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool TerminateProcess(IntPtr process, uint exitCode);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint WaitForSingleObject(IntPtr handle, uint timeoutMilliseconds);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);
}
