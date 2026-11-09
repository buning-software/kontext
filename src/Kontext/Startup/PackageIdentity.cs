using System.Runtime.InteropServices;

namespace Kontext.Startup;

internal static class PackageIdentity
{
    private const int AppModelErrorNoPackage = 15700;

    public static bool IsPackaged { get; } = DetectPackaged();

    private static bool DetectPackaged()
    {
        uint length = 0;
        return GetCurrentPackageFullName(ref length, null) != AppModelErrorNoPackage;
    }

#pragma warning disable SYSLIB1054
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetCurrentPackageFullName(ref uint packageFullNameLength, char[]? packageFullName);
#pragma warning restore SYSLIB1054
}
