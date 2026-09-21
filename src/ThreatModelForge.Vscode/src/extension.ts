import * as vscode from 'vscode';
import { basename, join } from 'node:path';
import { randomBytes } from 'node:crypto';
import { EngineWorker, MAX_DOCUMENT_BYTES, type Inspection } from './engine';
import { previewHtml } from './preview';
import { StudioEditors } from './studio';

interface DocumentState {
	version: number;
	result?: Inspection;
	pending?: Promise<Inspection>;
	status: string;
}

export function sourceFormat(document: vscode.TextDocument): string | undefined {
	const path = document.uri.path.toLowerCase();
	if (path.endsWith('.tm7')) return 'tm7';
	if (path.endsWith('.tmforge.json')) return 'tmforge-json';
	return undefined;
}

export function activate(context: vscode.ExtensionContext) {
	const engine = new EngineWorker(join(context.extensionPath, 'engine', '_framework'));
	const diagnostics = vscode.languages.createDiagnosticCollection('tmforge');
	const updates = new vscode.EventEmitter<string>();
	const states = new Map<string, DocumentState>();
	let disposed = false;
	const studio = new StudioEditors(context, document => { states.delete(document.uri.toString()); void inspect(document); });
	context.subscriptions.push(engine, studio, diagnostics, updates, { dispose: () => { disposed = true; states.clear(); } });

	const inspect = (document: vscode.TextDocument): Promise<Inspection> => {
		const key = document.uri.toString();
		const existing = states.get(key);
		if (existing?.version === document.version) {
			if (existing.pending) return existing.pending;
			if (existing.result) return Promise.resolve(existing.result);
		}
		const state: DocumentState = { version: document.version, status: 'Analyzing locally...' };
		states.set(key, state);
		diagnostics.delete(document.uri);
		updates.fire(key);
		state.pending = (async () => {
			let result: Inspection;
			try {
				if (!vscode.workspace.isTrusted) throw new Error('Trust this workspace before running model analysis.');
				const format = sourceFormat(document);
				if (!format) throw new Error('Open a .tm7 or .tmforge.json model.');
				const text = document.getText();
				if (Buffer.byteLength(text, 'utf8') > MAX_DOCUMENT_BYTES) throw new Error('Model inspection is limited to 8 MiB.');
				result = await (studio.engineFor(document) ?? engine).inspect(Buffer.from(text, 'utf8'), format);
			} catch (error) {
				result = { pages: [], analysis: null, diagnostics: [{
					code: 'inspection.failed', severity: 'error', path: '$',
					message: error instanceof Error ? error.message : 'Model inspection failed.',
				}] };
			}
			if (!disposed && !document.isClosed && document.version === state.version && states.get(key) === state) {
				state.pending = undefined;
				state.result = result;
				state.status = '';
				diagnostics.set(document.uri, toDiagnostics(result, document));
				updates.fire(key);
			}
			return result;
		})();
		return state.pending;
	};

	const provider: vscode.CustomTextEditorProvider = {
		async resolveCustomTextEditor(document, panel) {
			const media = vscode.Uri.joinPath(context.extensionUri, 'media');
			panel.webview.options = { enableScripts: true, localResourceRoots: [media] };
			const nonce = randomBytes(24).toString('base64');
			panel.webview.html = previewHtml(
				panel.webview.cspSource,
				panel.webview.asWebviewUri(vscode.Uri.joinPath(media, 'preview.js')).toString(),
				panel.webview.asWebviewUri(vscode.Uri.joinPath(media, 'preview.css')).toString(),
				nonce,
			);
			const key = document.uri.toString();
			const send = () => {
				const state = states.get(key);
				void panel.webview.postMessage({ type: 'update', title: basename(document.uri.path), status: state?.status ?? 'Analyzing locally...', result: state?.result });
			};
			const messages = panel.webview.onDidReceiveMessage((message: unknown) => {
				if (!message || typeof message !== 'object' || !('type' in message)) return;
				if (message.type === 'ready') send();
				if (message.type === 'reanalyze') { states.delete(key); void inspect(document); }
				if (message.type === 'source') void vscode.commands.executeCommand('vscode.openWith', document.uri, 'default');
				if (message.type === 'problems') void vscode.commands.executeCommand('workbench.actions.view.problems');
			});
			const changes = updates.event(changed => { if (changed === key) send(); });
			panel.onDidDispose(() => { messages.dispose(); changes.dispose(); });
			await inspect(document);
			send();
		},
	};

	context.subscriptions.push(
		vscode.window.registerCustomEditorProvider('tmforge.studio', studio, { supportsMultipleEditorsPerDocument: true }),
		vscode.commands.registerCommand('tmforge.newModel', () => studio.create()),
		vscode.commands.registerCommand('tmforge.openStudio', async (uri?: vscode.Uri) => {
			const target = uri ?? vscode.window.activeTextEditor?.document.uri;
			if (!target?.path.toLowerCase().endsWith('.tmforge.json')) { void vscode.window.showInformationMessage('Open a .tmforge.json model to edit in Studio.'); return; }
			await vscode.commands.executeCommand('vscode.openWith', target, 'tmforge.studio');
		}),
		vscode.window.registerCustomEditorProvider('tmforge.preview', provider, { webviewOptions: { retainContextWhenHidden: true } }),
		vscode.window.registerCustomEditorProvider('tmforge.jsonPreview', provider, { webviewOptions: { retainContextWhenHidden: true } }),
		vscode.commands.registerCommand('tmforge.openPreview', async (uri?: vscode.Uri) => {
			const target = uri ?? vscode.window.activeTextEditor?.document.uri;
			if (!target) { void vscode.window.showInformationMessage('Open a .tm7 or .tmforge.json model first.'); return; }
			const document = await vscode.workspace.openTextDocument(target);
			const format = sourceFormat(document);
			if (!format) { void vscode.window.showErrorMessage('Preview supports .tm7 and .tmforge.json models.'); return; }
			await vscode.commands.executeCommand('vscode.openWith', target, format === 'tm7' ? 'tmforge.preview' : 'tmforge.jsonPreview');
		}),
		vscode.commands.registerCommand('tmforge.analyze', async (uri?: vscode.Uri) => {
			const document = uri ? await vscode.workspace.openTextDocument(uri) : vscode.window.activeTextEditor?.document;
			if (!document || !sourceFormat(document)) { void vscode.window.showInformationMessage('Open a .tm7 or .tmforge.json model first.'); return; }
			states.delete(document.uri.toString());
			await inspect(document);
			await vscode.commands.executeCommand('workbench.actions.view.problems');
		}),
		vscode.workspace.onDidOpenTextDocument(document => { if (sourceFormat(document)) void inspect(document); }),
		vscode.workspace.onDidSaveTextDocument(document => { if (sourceFormat(document)) void inspect(document); }),
		vscode.workspace.onDidChangeTextDocument(event => {
			if (!sourceFormat(event.document) || event.contentChanges.length === 0) return;
			const key = event.document.uri.toString();
			states.set(key, { version: event.document.version, status: 'Model changed. Save or reanalyze for current findings.' });
			diagnostics.delete(event.document.uri);
			updates.fire(key);
		}),
		vscode.workspace.onDidCloseTextDocument(document => {
			states.delete(document.uri.toString());
			diagnostics.delete(document.uri);
		}),
		vscode.workspace.onDidGrantWorkspaceTrust(() => {
			for (const document of vscode.workspace.textDocuments) if (sourceFormat(document)) { states.delete(document.uri.toString()); void inspect(document); }
		}),
	);
	for (const document of vscode.workspace.textDocuments) if (sourceFormat(document)) void inspect(document);
	return { inspect, diagnostics, edit: studio.edit.bind(studio), waitUntilRendered: studio.waitUntilRendered.bind(studio) };
}

function toDiagnostics(result: Inspection, document: vscode.TextDocument): vscode.Diagnostic[] {
	const range = document.lineAt(0).range;
	const make = (message: string, severity: string, code: string) => {
		const diagnostic = new vscode.Diagnostic(range, message, severity === 'error' ? vscode.DiagnosticSeverity.Error
			: severity === 'warning' ? vscode.DiagnosticSeverity.Warning : vscode.DiagnosticSeverity.Information);
		diagnostic.source = 'tmforge';
		diagnostic.code = code;
		return diagnostic;
	};
	return [
		...result.diagnostics.map(item => make(`${item.path}: ${item.message}`, item.severity, item.code)),
		...(result.analysis?.diagnostics ?? []).map(message => make(message, 'error', 'analysis.incomplete')),
		...(result.analysis?.findings ?? []).map(finding => make(
			`${finding.message}${finding.elementIds.length ? '\nElements: ' + finding.elementIds.join(', ') : ''}`,
			finding.id === 'engine-error' || finding.ruleId === 'rule-pack-mismatch' ? 'error' : finding.severity,
			finding.ruleId ?? finding.id,
		)),
	];
}
