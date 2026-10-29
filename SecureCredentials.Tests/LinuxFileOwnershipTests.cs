
using System.Diagnostics;
using SecureCredentials;
using Xunit;

namespace SecureCredentials.Tests;

public class LinuxFileOwnershipTests
{
    [Fact]
    public void GetOwnerUserId_OnLinux_MatchesSystemStat()
    {
        if (!OperatingSystem.IsLinux())
            return;

        // Consultar el propietario de nuestro directorio personal.
        string homeDirectory =
            Environment.GetFolderPath(
                Environment.SpecialFolder.UserProfile);

        // Obtener UID mediante nuestra biblioteca.
        uint libraryUid =
            LinuxFileOwnership.GetOwnerUserId(homeDirectory);

        // Obtener UID mediante el comando stat de Linux.
        using Process process = new();

        process.StartInfo = new ProcessStartInfo
        {
            FileName = "/usr/bin/stat",
            RedirectStandardOutput = true,
            UseShellExecute = false
        };

        process.StartInfo.ArgumentList.Add("-c");
        process.StartInfo.ArgumentList.Add("%u");
        process.StartInfo.ArgumentList.Add(homeDirectory);

        Assert.True(process.Start());

        string output = process.StandardOutput.ReadToEnd();

        process.WaitForExit();

        Assert.Equal(0, process.ExitCode);

        uint systemUid = uint.Parse(output.Trim());

        // Ambos métodos deben devolver el mismo UID.
        Assert.Equal(systemUid, libraryUid);
    }

    // PRUEBA 17: Aceptar cuando los UID coinciden.
    [Fact]
    public void ValidateOwner_WithMatchingUids_DoesNotThrow()
    {
        LinuxFileOwnership.ValidateOwner(
            effectiveUid: 1000,
            ownerUid: 1000);
    }

    // PRUEBA 18: Rechazar cuando los UID son diferentes.
    [Fact]
    public void ValidateOwner_WithDifferentUids_Throws()
    {
        Assert.Throws<UnauthorizedAccessException>(() =>
            LinuxFileOwnership.ValidateOwner(
                effectiveUid: 1000,
                ownerUid: 0));
    }



}
