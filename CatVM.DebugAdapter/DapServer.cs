using System.Text.Json.Nodes;
using CatVM.DebugAdapter.Protocol;

namespace CatVM.DebugAdapter;

/// <summary>
/// Entry point: serves one Debug Adapter Protocol session over the given streams and returns
/// when the client disconnects or the input ends.
/// </summary>
public static class DapServer {
    public static int Run(Stream input, Stream output, IVmLauncher launcher) {
        DapTransport transport = new(input, output);

        // From here on nothing may print to stdout except the protocol: guest output, device
        // loader chatter and fault dumps all become `output` events instead.
        OutputEventWriter outputWriter = new(transport);
        Console.SetOut(outputWriter);

        DapSession session = new(transport, launcher, outputWriter);
        Log.Stderr("CatVM debug adapter ready");

        try {
            while (!session.ShouldExit && transport.TryRead(out JsonObject? message)) {
                if (message == null) continue;
                session.Handle(message);
            }
        }
        finally {
            // The client may have gone away without a disconnect; make sure the VM stops.
            session.Shutdown();
        }

        Log.Stderr("CatVM debug adapter exiting");
        return 0;
    }
}
