import { cpSync, existsSync, rmSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { spawnSync } from 'node:child_process';

const docsDirectory = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const project = resolve(docsDirectory, '../CatMachine.Playground/CatMachine.Playground.csproj');
const publishDirectory = join(docsDirectory, '.playground-publish');
const frameworkSource = join(publishDirectory, 'wwwroot/_framework');
const frameworkDestination = join(docsDirectory, 'public/playground-runtime/_framework');

rmSync(publishDirectory, { recursive: true, force: true });

const publish = spawnSync(
	'dotnet',
	['publish', project, '--configuration', 'Release', '--output', publishDirectory, '--nologo'],
	{ stdio: 'inherit' },
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
