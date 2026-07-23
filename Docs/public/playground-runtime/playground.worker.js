let runtimePromise;
let runAssembly;
let activeRequestId;

async function loadRuntime() {
	if (!runtimePromise) {
		self.postMessage({ type: 'status', status: 'loading' });
		runtimePromise = import('./_framework/dotnet.js')
			.then(async ({ dotnet }) => {
				const runtime = await dotnet
					.withDiagnosticTracing(false)
					.create();
				runtime.setModuleImports('playground', {
					postOutput(output) {
						self.postMessage({ type: 'output', id: activeRequestId, output });
					},
				});
				const config = runtime.getConfig();
				const exports = await runtime.getAssemblyExports(config.mainAssemblyName);
				runAssembly = exports.CatMachine.Playground.Program.Run;
				await runtime.runMain();
				self.postMessage({ type: 'status', status: 'ready' });
			})
			.catch((error) => {
				runtimePromise = undefined;
				throw error;
			});
	}

	await runtimePromise;
}

self.addEventListener('message', async (event) => {
	if (event.data?.type !== 'run') return;

	try {
		await loadRuntime();
		activeRequestId = event.data.id;
		const result = JSON.parse(runAssembly(
			event.data.entryFile,
			JSON.stringify({ Files: event.data.files }),
		));
		self.postMessage({ type: 'result', id: event.data.id, result });
	} catch (error) {
		self.postMessage({
			type: 'error',
			id: event.data.id,
			message: error instanceof Error ? error.message : String(error),
		});
	} finally {
		activeRequestId = undefined;
	}
});
