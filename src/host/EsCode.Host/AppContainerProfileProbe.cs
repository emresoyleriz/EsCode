using System.ComponentModel;
using System.Runtime.InteropServices;

// G2.1: Windows AppContainer profile availability *probe*, not a process launcher.
// A successful profile creation says NOTHING about safe worker isolation.
// Never expose profile creation to renderer/model/plugin-controlled input.
internal static class AppContainerProfileProbe
{
    internal static bool Run()
    {
        if (!OperatingSystem.IsWindows()) return false;
        var name = "EsCode.Probe." + Guid.NewGuid().ToString("N");
        IntPtr createdSid = IntPtr.Zero, derivedSid = IntPtr.Zero;
        var wasCreated = false;
        try
        {
            var hr = CreateAppContainerProfile(name, "EsCode CI isolation probe",
                "Disposable test-only profile. No worker is started.", IntPtr.Zero, 0, out createdSid);
            if (hr != 0)
            {
                // Absence of AppContainer support/permissions MUST block real tool execution.
                // No fallback to a normal user token or only a Job Object is allowed.
                return false;
            }
            wasCreated = true;
            if (createdSid == IntPtr.Zero) return false;

            var derive = DeriveAppContainerSidFromAppContainerName(name, out derivedSid);
            if (derive != 0 || derivedSid == IntPtr.Zero) return false;
            if (!EqualSid(createdSid, derivedSid)) return false;

            // This smoke intentionally does NOT start arbitrary programs, access files
            // or use the network. Those require separate hostile-VM negative tests.
            return true;
        }
        finally
        {
            if (derivedSid != IntPtr.Zero) FreeSid(derivedSid);
            if (createdSid != IntPtr.Zero) FreeSid(createdSid);
            if (wasCreated)
            {
                var deleted = DeleteAppContainerProfile(name);
                if (deleted != 0)
                    throw new Win32Exception($"AppContainer test profile cleanup failed (HRESULT 0x{deleted:X8})");
            }
        }
    }

    [DllImport("userenv.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int CreateAppContainerProfile(
        string pszAppContainerName, string pszDisplayName, string pszDescription,
        IntPtr capabilities, uint capabilityCount, out IntPtr appContainerSid);

    [DllImport("userenv.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int DeriveAppContainerSidFromAppContainerName(
        string pszAppContainerName, out IntPtr appContainerSid);

    [DllImport("userenv.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int DeleteAppContainerProfile(string pszAppContainerName);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EqualSid(IntPtr left, IntPtr right);

    [DllImport("advapi32.dll")]
    private static extern IntPtr FreeSid(IntPtr sid);
}
