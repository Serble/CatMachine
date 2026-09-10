// Registers the CatVM debug adapter. The adapter is `catlaunch dap`, talking the Debug Adapter
// Protocol over stdio; this extension only tells VS Code how to start it.
const vscode = require("vscode");

/** Splits `catvm.catlaunchPath` into command + arguments so a source-checkout command such as
 *  `dotnet run --project /path/CatLauncher.csproj --` works as well as a plain `catlaunch`. */
function launcherCommand() {
    const setting = vscode.workspace.getConfiguration("catvm").get("catlaunchPath", "catlaunch").trim();
    const parts = setting.match(/(?:[^\s"]+|"[^"]*")+/g) || ["catlaunch"];
    const unquote = (s) => s.replace(/^"(.*)"$/, "$1");
    return { command: unquote(parts[0]), args: parts.slice(1).map(unquote) };
}

class CatVmAdapterFactory {
    createDebugAdapterDescriptor(_session) {
        const { command, args } = launcherCommand();
        return new vscode.DebugAdapterExecutable(command, [...args, "dap"]);
    }
}

class CatVmConfigurationProvider {
    /** Fills in a usable configuration when F5 is pressed with no launch.json. */
    resolveDebugConfiguration(folder, config) {
        if (!config.type && !config.request && !config.name) {
            const editor = vscode.window.activeTextEditor;
            if (editor && (editor.document.languageId === "catasm" || editor.document.languageId === "catnip")) {
                config.type = "catvm";
                config.request = "launch";
                config.name = "Debug ROM";
                config.program = "${workspaceFolder}/bin/main.bin";
                config.cwd = "${workspaceFolder}";
                config.fast = true;
                config.stopOnEntry = true;
            }
        }

        if (!config.program) {
            return vscode.window.showInformationMessage("Set `program` to the ROM to debug in launch.json.").then(() => undefined);
        }

        return config;
    }
}

function activate(context) {
    context.subscriptions.push(
        vscode.debug.registerDebugAdapterDescriptorFactory("catvm", new CatVmAdapterFactory()),
        vscode.debug.registerDebugConfigurationProvider("catvm", new CatVmConfigurationProvider())
    );
}

function deactivate() {}

module.exports = { activate, deactivate };
