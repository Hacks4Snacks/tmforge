import { mkdtemp, rm, access } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { runTests } from '@vscode/test-electron';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const directory = await mkdtemp(resolve(tmpdir(), 'tmforge-extension-host-'));
let executable = process.env.VSCODE_EXECUTABLE_PATH;
if (!executable && process.platform === 'darwin') {
  const local = '/Applications/Visual Studio Code.app/Contents/MacOS/Electron';
  try { await access(local); executable = local; } catch {}
}
let passed = false;
try {
  await runTests({
    vscodeExecutablePath: executable,
    extensionDevelopmentPath: root,
    extensionTestsPath: resolve(root, 'out/test/extension.test.js'),
    launchArgs: [
      '--user-data-dir=' + resolve(directory, 'profile'),
      '--extensions-dir=' + resolve(directory, 'extensions'),
      '--disable-extensions', '--disable-workspace-trust', '--skip-welcome', '--skip-release-notes',
    ],
  });
  passed = true;
} finally {
  if (passed) await rm(directory, { recursive: true, force: true });
  else console.error('Extension-host failure logs:', directory);
}
