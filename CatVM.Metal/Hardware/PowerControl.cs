using System.Diagnostics;

namespace CatVM.Metal.Hardware;

/// <summary>
/// Translates the guest's power requests into real ones.
/// <p/>
/// On the Metal machine there is no window to close and no shell to return to, so a guest asking the
/// hardware manager to shut down has to turn the physical machine off, otherwise the request looks
/// like a hang. The host's own tools are used rather than the <c>reboot</c> syscall so that the init
/// system gets to unmount filesystems first.
/// </summary>
public static class PowerControl {
    /// <summary>What the guest asked the machine to do once the CPU stopped.</summary>
    public enum Action {
        None,
        PowerOff,
        Reboot
    }

    public static void Perform(Action action) {
        switch (action) {
            case Action.PowerOff:
                Log.Info("powering off");
                Sync();
                Run(["poweroff", "/sbin/poweroff", "/sbin/openrc-shutdown", "halt"], "-p");
                break;

            case Action.Reboot:
                Log.Info("rebooting");
                Sync();
                Run(["reboot", "/sbin/reboot", "/sbin/openrc-shutdown"], "-r");
                break;

            case Action.None:
            default:
                break;
        }
    }

    private static void Sync() {
        Run(["sync", "/bin/sync"], null, wait: true);
    }

    /// <summary>
    /// Runs the first of the given commands that can be started.
    /// </summary>
    /// <param name="commands">Commands to try, in order of preference.</param>
    /// <param name="openrcArgument">
    /// Argument used only for <c>openrc-shutdown</c>, which unlike the others needs to be told what
    /// to do.
    /// </param>
    /// <param name="wait">Whether to wait for the command to finish.</param>
    private static void Run(string[] commands, string? openrcArgument, bool wait = false) {
        foreach (string command in commands) {
            ProcessStartInfo info = new(command) {
                UseShellExecute = false
            };

            if (openrcArgument != null && command.EndsWith("openrc-shutdown", StringComparison.Ordinal)) {
                info.ArgumentList.Add(openrcArgument);
                info.ArgumentList.Add("now");
            }

            try {
                using Process? process = Process.Start(info);
                if (process == null) {
                    continue;
                }

                if (wait) {
                    process.WaitForExit(5000);
                }

                return;
            }
            catch (Exception ex) {
                Log.Warn($"could not run {command}: {ex.Message}");
            }
        }

        Log.Error("no way to change the machine's power state was available");
    }
}
