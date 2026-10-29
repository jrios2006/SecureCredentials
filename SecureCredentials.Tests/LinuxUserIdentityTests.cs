
using System.Diagnostics;
using SecureCredentials;
using Xunit;

namespace SecureCredentials.Tests;

public class LinuxUserIdentityTests
{
    [Fact]
    public void GetEffectiveUserId_OnLinux_MatchesSystemId()
    {
        if (!OperatingSystem.IsLinux())
            return;

        // Obtener el UID efectivo mediante nuestra biblioteca.
        uint libraryUid = LinuxUserIdentity.GetEffectiveUserId();

        // Obtener el UID efectivo mediante el comando del sistema.
        using Process process = new();

        process.StartInfo = new ProcessStartInfo
        {
            FileName = "/usr/bin/id",
            Arguments = "-u",
            RedirectStandardOutput = true,
            UseShellExecute = false
        };

        Assert.True(process.Start());

        string output = process.StandardOutput.ReadToEnd();

        process.WaitForExit();

        Assert.Equal(0, process.ExitCode);

        uint systemUid = uint.Parse(output.Trim());

        // Ambos resultados deben coincidir.
        Assert.Equal(systemUid, libraryUid);
    }
}
