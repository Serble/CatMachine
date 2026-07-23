import { chmodSync, cpSync, existsSync, mkdirSync, rmSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { spawnSync } from 'node:child_process';

const docsDirectory = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const project = resolve(docsDirectory, '../CatMachine.Playground/CatMachine.Playground.csproj');
const publishDirectory = join(docsDirectory, '.playground-publish');
const frameworkSource = join(publishDirectory, 'wwwroot/_framework');
const frameworkDestination = join(docsDirectory, 'public/playground-runtime/_framework');

// The playground targets net10.0, so any SDK whose major version is >= this is usable.
const requiredSdkMajor = 10;
// Channel used when we have to install the SDK ourselves (e.g. on Cloudflare Pages).
const installChannel = process.env.DOTNET_INSTALL_CHANNEL ?? '10.0';
const localDotnetRoot = join(docsDirectory, '.dotnet');
const isWindows = process.platform === 'win32';
const localDotnetExe = join(localDotnetRoot, isWindows ? 'dotnet.exe' : 'dotnet');

function run(command, args, options = {}) {
	return spawnSync(command, args, { stdio: 'inherit', ...options });
}

function capture(command, args, options = {}) {
	const result = spawnSync(command, args, { encoding: 'utf8', ...options });
	if (result.error || result.status !== 0) return null;
	return result.stdout ?? '';
}

// Returns true when the given dotnet command exposes an SDK new enough to build the playground.
function hasCompatibleSdk(dotnetCommand, env = process.env) {
	const listed = capture(dotnetCommand, ['--list-sdks'], { env });
	if (listed === null) return false;
	return listed
		.split('\n')
		.map((line) => Number.parseInt(line.trim().split('.')[0], 10))
		.some((major) => Number.isInteger(major) && major >= requiredSdkMajor);
}

function download(url, destination) {
	if (capture('curl', ['--fail', '--silent', '--show-error', '--location', '--output', destination, url]) !== null) {
		return true;
	}
	return capture('wget', ['--quiet', '--output-document', destination, url]) !== null;
}

// Downloads the official dotnet-install script and installs the SDK into a repo-local directory.
function installLocalDotnet() {
	console.log(`[build-playground] No compatible .NET SDK found. Installing channel ${installChannel} into ${localDotnetRoot}`);
	mkdirSync(localDotnetRoot, { recursive: true });
	const scriptUrl = isWindows
		? 'https://dot.net/v1/dotnet-install.ps1'
		: 'https://dot.net/v1/dotnet-install.sh';
	const scriptPath = join(localDotnetRoot, isWindows ? 'dotnet-install.ps1' : 'dotnet-install.sh');

	if (!download(scriptUrl, scriptPath)) {
		throw new Error('Failed to download the dotnet-install script. Ensure curl or wget is available.');
	}

	let install;
	if (isWindows) {
		install = run('powershell', [
			'-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', scriptPath,
			'-Channel', installChannel, '-InstallDir', localDotnetRoot,
		]);
	} else {
		chmodSync(scriptPath, 0o755);
		install = run('bash', [scriptPath, '--channel', installChannel, '--install-dir', localDotnetRoot]);
	}

	if (install.error || install.status !== 0) {
		throw install.error ?? new Error('dotnet-install script failed.');
	}
	if (!existsSync(localDotnetExe)) {
		throw new Error('dotnet-install completed but the dotnet executable was not found.');
	}
}

// Resolves the dotnet command to use, installing a local SDK when necessary.
function resolveDotnet() {
	if (hasCompatibleSdk('dotnet')) {
		return { command: 'dotnet', env: process.env };
	}
	if (!existsSync(localDotnetExe) || !hasCompatibleSdk(localDotnetExe)) {
		installLocalDotnet();
	}
	const env = {
		...process.env,
		DOTNET_ROOT: localDotnetRoot,
		PATH: `${localDotnetRoot}${isWindows ? ';' : ':'}${process.env.PATH ?? ''}`,
	};
	return { command: localDotnetExe, env };
}

// The WebAssembly SDK requires the wasm-tools workload to publish.
function ensureWasmWorkload(dotnet) {
	const listed = capture(dotnet.command, ['workload', 'list'], { env: dotnet.env });
	if (listed !== null && /\bwasm-tools\b/.test(listed)) return;
	console.log('[build-playground] Installing the wasm-tools workload.');
	const install = run(dotnet.command, ['workload', 'install', 'wasm-tools'], { env: dotnet.env });
	if (install.error || install.status !== 0) {
		throw install.error ?? new Error('Failed to install the wasm-tools workload.');
	}
}

const dotnet = resolveDotnet();
ensureWasmWorkload(dotnet);

rmSync(publishDirectory, { recursive: true, force: true });

const publish = run(
	dotnet.command,
	['publish', project, '--configuration', 'Release', '--output', publishDirectory, '--nologo'],
	{ env: dotnet.env },
);

if (publish.error) {
	throw publish.error;
}

if (publish.status !== 0 || !existsSync(frameworkSource)) {
	process.exit(publish.status || 1);
}

rmSync(frameworkDestination, { recursive: true, force: true });
cpSync(frameworkSource, frameworkDestination, { recursive: true });
rmSync(publishDirectory, { recursive: true, force: true });
