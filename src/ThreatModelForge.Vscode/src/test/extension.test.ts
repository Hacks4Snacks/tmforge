import assert from 'node:assert/strict';
import { mkdtemp, readFile, rm, writeFile } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join, resolve } from 'node:path';
import * as vscode from 'vscode';
import { EngineWorker, type Inspection } from '../engine';

export async function run(): Promise<void> {
	const extension = vscode.extensions.getExtension<{
		inspect(document: vscode.TextDocument): Promise<Inspection>;
		diagnostics: vscode.DiagnosticCollection;
		edit(document: vscode.TextDocument, version: number, previous: unknown, next: unknown): Promise<{ version: number; dirty: boolean }>;
		waitUntilRendered(document: vscode.TextDocument): Promise<void>;
	}>('hacks4snacks.tmforge');
	assert.ok(extension, 'Extension was not loaded');
	const api = await extension.activate();
	const directory = await mkdtemp(join(tmpdir(), 'tmforge-vscode-test-'));
	try {
		const source = await readFile(resolve(__dirname, '../../../../examples/webshop.tm7'));
		const path = join(directory, 'webshop.tm7');
		await writeFile(path, source);
		const uri = vscode.Uri.file(path);
		const document = await vscode.workspace.openTextDocument(uri);
		const result = await api.inspect(document);
		assert.equal(result.pages.length, 1);
		assert.ok(api.diagnostics.get(uri)?.some(diagnostic => diagnostic.code === 'TM1029'));
		await vscode.commands.executeCommand('tmforge.openPreview', uri);
		assert.ok(vscode.window.tabGroups.all.flatMap(group => group.tabs).some(tab => tab.input instanceof vscode.TabInputCustom && tab.input.viewType === 'tmforge.preview'));
		assert.deepEqual(await readFile(path), source, 'Preview mutated the source');
		const jsonPath = join(directory, 'save.tmforge.json');
		const invalid = '{"schema":"tmforge-json","elements":[],"flows":[{"id":"broken","source":"missing","target":"missing"}]}';
		await writeFile(jsonPath, invalid);
		const jsonDocument = await vscode.workspace.openTextDocument(jsonPath);
		await api.inspect(jsonDocument);
		assert.ok(api.diagnostics.get(jsonDocument.uri)?.some(diagnostic => diagnostic.severity === vscode.DiagnosticSeverity.Error));
		const valid = '{"schema":"tmforge-json","elements":[{"id":"api","kind":"process","name":"API","x":10,"y":10}],"flows":[]}';
		const edits = new vscode.WorkspaceEdit();
		edits.replace(jsonDocument.uri, new vscode.Range(jsonDocument.positionAt(0), jsonDocument.positionAt(invalid.length)), valid);
		assert.ok(await vscode.workspace.applyEdit(edits));
		const updated = new Promise<void>((resolveUpdate, reject) => {
			const timer = setTimeout(() => { subscription.dispose(); reject(new Error('Save did not update Problems diagnostics')); }, 15000);
			const subscription = vscode.languages.onDidChangeDiagnostics(event => {
				if (event.uris.some(candidate => candidate.toString() === jsonDocument.uri.toString()) && api.diagnostics.get(jsonDocument.uri)?.some(diagnostic => String(diagnostic.code).startsWith('TM'))) {
					clearTimeout(timer); subscription.dispose(); resolveUpdate();
				}
			});
		});
		await jsonDocument.save();
		await updated;
		assert.ok(!api.diagnostics.get(jsonDocument.uri)?.some(diagnostic => String(diagnostic.code).startsWith('model.')));
		await vscode.commands.executeCommand('tmforge.openPreview', jsonDocument.uri);
		assert.ok(vscode.window.tabGroups.all.flatMap(group => group.tabs).some(tab => tab.input instanceof vscode.TabInputCustom && tab.input.viewType === 'tmforge.jsonPreview'));
		await vscode.commands.executeCommand('tmforge.openStudio', jsonDocument.uri);
		await api.waitUntilRendered(jsonDocument);
		assert.ok(vscode.window.tabGroups.all.flatMap(group => group.tabs).some(tab => tab.input instanceof vscode.TabInputCustom && tab.input.viewType === 'tmforge.studio'));
		assert.equal(jsonDocument.getText(), valid, 'Opening Studio changed the source');
		assert.equal(jsonDocument.isDirty, false);
		const previous = JSON.parse(valid);
		const next = structuredClone(previous);
		next.elements[0].name = 'Renamed in Studio';
		const version = jsonDocument.version;
		const edited = await api.edit(jsonDocument, version, previous, next);
		assert.equal(edited.dirty, true);
		assert.equal(JSON.parse(jsonDocument.getText()).elements[0].name, 'Renamed in Studio');
		await assert.rejects(api.edit(jsonDocument, version, previous, next), /another editor/);
		const editedText = jsonDocument.getText();
		await vscode.commands.executeCommand('undo');
		await waitForText(jsonDocument, valid);
		assert.equal(jsonDocument.getText(), valid, 'Undo did not restore the exact original text');
		await vscode.commands.executeCommand('redo');
		await waitForText(jsonDocument, editedText);
		assert.equal(JSON.parse(jsonDocument.getText()).elements[0].name, 'Renamed in Studio');
		await vscode.window.showTextDocument(jsonDocument);
		await vscode.commands.executeCommand('tmforge.openStudio', jsonDocument.uri);
		await api.waitUntilRendered(jsonDocument);
		assert.equal(jsonDocument.isDirty, true, 'Reopening Studio lost unsaved document state');
		assert.equal(JSON.parse(jsonDocument.getText()).elements[0].name, 'Renamed in Studio');
		const invalidEdit = { ...next, flows: [{ id: 'broken', source: 'api', target: 'missing' }] };
		await assert.rejects(api.edit(jsonDocument, jsonDocument.version, next, invalidEdit), /missing/i);
		assert.equal(JSON.parse(jsonDocument.getText()).flows.length, 0);
		await jsonDocument.save();
		assert.equal(JSON.parse(await readFile(jsonPath, 'utf8')).elements[0].name, 'Renamed in Studio');
		assert.equal(jsonDocument.isDirty, false);
		await vscode.commands.executeCommand('workbench.action.splitEditorRight');
		await api.waitUntilRendered(jsonDocument);
		const externalText = jsonDocument.getText().replace('Renamed in Studio', 'Changed from source');
		const externalEdit = new vscode.WorkspaceEdit();
		externalEdit.replace(jsonDocument.uri, new vscode.Range(jsonDocument.positionAt(0), jsonDocument.positionAt(jsonDocument.getText().length)), externalText);
		await vscode.workspace.applyEdit(externalEdit);
		await api.waitUntilRendered(jsonDocument);
		await jsonDocument.save();
		assert.equal(JSON.parse(await readFile(jsonPath, 'utf8')).elements[0].name, 'Changed from source');
		await assert.rejects(api.edit(document, document.version, previous, next), /only .tmforge.json/);
		assert.deepEqual(await readFile(path), source, 'Studio modified the native TM7');
		await vscode.commands.executeCommand('tmforge.newModel');
		const newModel = vscode.workspace.textDocuments.find(candidate => candidate.isUntitled && candidate.uri.path.endsWith('.tmforge.json'));
		assert.ok(newModel, 'New Model did not create an untitled JSON document');
		await api.waitUntilRendered(newModel);
		assert.equal(newModel.isDirty, true);
		assert.deepEqual(JSON.parse(newModel.getText()).elements, []);
		await vscode.commands.executeCommand('workbench.action.revertAndCloseActiveEditor');

		const originalInspect = EngineWorker.prototype.inspect;
		let finish!: (inspection: Inspection) => void;
		EngineWorker.prototype.inspect = () => new Promise(resolveInspection => { finish = resolveInspection; });
		try {
			const stalePath = join(directory, 'stale.tmforge.json');
			await writeFile(stalePath, valid);
			const staleDocument = await vscode.workspace.openTextDocument(stalePath);
			const pending = api.inspect(staleDocument);
			const edit = new vscode.WorkspaceEdit();
			edit.insert(staleDocument.uri, new vscode.Position(0, 0), ' ');
			await vscode.workspace.applyEdit(edit);
			finish(result);
			await pending;
			assert.equal(api.diagnostics.get(staleDocument.uri)?.length ?? 0, 0, 'A stale analysis result reached Problems');
			const current = api.inspect(staleDocument);
			finish(result);
			await current;
			assert.ok(api.diagnostics.get(staleDocument.uri)?.length, 'A current result was not published');
			await staleDocument.save();
		} finally { EngineWorker.prototype.inspect = originalInspect; }

		await vscode.extensions.getExtension('vscode.json-language-features')?.activate();
		const schemaCases = [
			['example.manifest.json', '{"elements":[{"kind":"process","properties":{}}]}', 'properties'],
			['example.suppressions.json', '{"files":[{"file":"model.tm7","suppressions":[{"ruleID":"TM1021"}]}]}', 'ruleID'],
			['example.tmrules.json', '{"schema":"tmforge-rules","version":99,"dialect":"urn:tmforge:rules:flat-v1","pack":{"id":"example","name":"Example"},"rules":[]}', '2'],
		];
		for (const [name, text, expected] of schemaCases) {
			const path = join(directory, name);
			await writeFile(path, text);
			const document = await vscode.workspace.openTextDocument(path);
			await vscode.window.showTextDocument(document);
			await waitForDiagnostics(document.uri, messages => messages.some(message => message.message.includes(expected)));
		}
		console.log('Extension-host tests passed: native preview, canonical Studio, unchanged open, dirty edits, undo/redo, save, invalid/stale-edit rejection, TM7 protection, save diagnostics, and local schemas.');
	} catch (error) {
		console.error('Tmforge extension test failure:', error);
		throw error;
	} finally {
		await vscode.commands.executeCommand('workbench.action.closeAllEditors');
		await rm(directory, { recursive: true, force: true });
	}
}

function waitForText(document: vscode.TextDocument, expected: string): Promise<void> {
	if (document.getText() === expected) return Promise.resolve();
	return new Promise((resolveWait, reject) => {
		const timer = setTimeout(() => { subscription.dispose(); reject(new Error('Document did not reach the expected text: ' + document.getText())); }, 5000);
		const subscription = vscode.workspace.onDidChangeTextDocument(event => {
			if (event.document === document && document.getText() === expected) { clearTimeout(timer); subscription.dispose(); resolveWait(); }
		});
	});
}

function waitForDiagnostics(uri: vscode.Uri, predicate: (diagnostics: readonly vscode.Diagnostic[]) => boolean): Promise<void> {
	if (predicate(vscode.languages.getDiagnostics(uri))) return Promise.resolve();
	return new Promise((resolveWait, reject) => {
		const timer = setTimeout(() => {
			subscription.dispose();
			reject(new Error('Expected schema diagnostics for ' + uri.path + ': ' + JSON.stringify(vscode.languages.getDiagnostics(uri))));
		}, 15000);
		const subscription = vscode.languages.onDidChangeDiagnostics(event => {
			if (event.uris.some(candidate => candidate.toString() === uri.toString()) && predicate(vscode.languages.getDiagnostics(uri))) {
				clearTimeout(timer); subscription.dispose(); resolveWait();
			}
		});
	});
}
