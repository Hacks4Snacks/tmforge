import * as vscode from 'vscode';
import { randomBytes, randomUUID } from 'node:crypto';
import { basename, join } from 'node:path';
import { EngineWorker, MAX_DOCUMENT_BYTES, MAX_REQUEST_BYTES } from './engine';
import { applyModelChange, textChange } from './document';
import { studioHtml } from './preview';

interface Session {
	document: vscode.TextDocument;
	engine: EngineWorker;
	panels: Set<vscode.WebviewPanel>;
	queue: Promise<void>;
	origin?: vscode.WebviewPanel;
	rendered: Map<vscode.WebviewPanel, number>;
	ready?: boolean;
	error?: string;
}

export class StudioEditors implements vscode.CustomTextEditorProvider, vscode.Disposable {
	private readonly sessions = new Map<string, Session>();
	private readonly subscriptions: vscode.Disposable[];

	constructor(private readonly context: vscode.ExtensionContext, private readonly reanalyze: (document: vscode.TextDocument) => void) {
		this.subscriptions = [
			vscode.workspace.onDidChangeTextDocument(event => {
				const session = this.sessions.get(event.document.uri.toString());
				if (!session) return;
				if (event.contentChanges.length) void this.publish(session, session.origin);
				else this.status(session);
			}),
			vscode.workspace.onDidSaveTextDocument(document => {
				const session = this.sessions.get(document.uri.toString());
				if (session) this.status(session);
			}),
			vscode.workspace.onDidCloseTextDocument(document => {
				const key = document.uri.toString();
				this.sessions.get(key)?.engine.dispose();
				this.sessions.delete(key);
			}),
			vscode.window.onDidChangeActiveColorTheme(() => {
				for (const session of this.sessions.values()) this.status(session);
			}),
		];
	}

	engineFor(document: vscode.TextDocument): EngineWorker | undefined {
		return this.sessions.get(document.uri.toString())?.engine;
	}

	private session(document: vscode.TextDocument): Session {
		const key = document.uri.toString();
		let session = this.sessions.get(key);
		if (!session) {
			session = { document, engine: new EngineWorker(join(this.context.extensionPath, 'engine', '_framework')), panels: new Set(), queue: Promise.resolve(), rendered: new Map() };
			this.sessions.set(key, session);
		}
		return session;
	}

	async resolveCustomTextEditor(document: vscode.TextDocument, panel: vscode.WebviewPanel): Promise<void> {
		const session = this.session(document);
		session.panels.add(panel);
		const root = vscode.Uri.joinPath(this.context.extensionUri, 'media', 'studio');
		panel.webview.options = { enableScripts: true, localResourceRoots: [root] };
		panel.webview.html = studioHtml(panel.webview.cspSource,
			panel.webview.asWebviewUri(vscode.Uri.joinPath(root, 'studio.js')).toString(),
			panel.webview.asWebviewUri(vscode.Uri.joinPath(root, 'studio.css')).toString(), randomBytes(24).toString('base64'));
		const messages = panel.webview.onDidReceiveMessage((message: unknown) => {
			if (!message || typeof message !== 'object' || !('type' in message)) return;
			if (message.type === 'ready') { session.ready = true; void this.publish(session); return; }
			if (message.type === 'renderError' && 'error' in message) { session.error = String(message.error); return; }
			if (message.type === 'rendered' && 'version' in message && typeof message.version === 'number') {
				session.rendered.set(panel, message.version);
				return;
			}
			void this.receive(session, panel, message as Record<string, unknown>);
		});
		const visibility = panel.onDidChangeViewState(() => { if (!panel.visible) session.rendered.delete(panel); });
		panel.onDidDispose(() => { session.panels.delete(panel); session.rendered.delete(panel); messages.dispose(); visibility.dispose(); });
	}

	async waitUntilRendered(document: vscode.TextDocument): Promise<void> {
		const session = this.session(document);
		const deadline = Date.now() + 15000;
		const rendered = () => {
			const visible = [...session.panels].filter(panel => panel.visible);
			return visible.length > 0 && visible.every(panel => session.rendered.get(panel) === document.version);
		};
		while (!rendered()) {
			if (Date.now() >= deadline) throw new Error(`Studio did not render the current document (ready=${session.ready}, error=${session.error ?? 'none'}).`);
			await new Promise(resolve => setTimeout(resolve, 25));
		}
	}

	private status(session: Session): void {
		for (const panel of session.panels) void panel.webview.postMessage({ type: 'status', ...this.documentStatus(session.document) });
	}

	private documentStatus(document: vscode.TextDocument) {
		return { version: document.version, dirty: document.isDirty, fileName: basename(document.uri.path),
			theme: vscode.window.activeColorTheme.kind === vscode.ColorThemeKind.Dark || vscode.window.activeColorTheme.kind === vscode.ColorThemeKind.HighContrast ? 'dark' : 'light' };
	}

	private async preflight(engine: EngineWorker, text: string): Promise<void> {
		if (Buffer.byteLength(text, 'utf8') > MAX_DOCUMENT_BYTES) throw new Error('Model documents are limited to 8 MiB.');
		const result = JSON.parse(await engine.invoke('Preflight', [Buffer.from(text).toString('base64'), 'tmforge-json', ''])) as {
			success: boolean; diagnostics: { severity: string; path: string; message: string }[];
		};
		if (!result.success) throw new Error(result.diagnostics.filter(item => item.severity === 'error').map(item => `${item.path}: ${item.message}`).join('\n') || 'The model is invalid.');
		const model = JSON.parse(text) as { diagrams?: { elements?: unknown[]; flows?: unknown[] }[]; elements?: unknown[]; flows?: unknown[] };
		const pages = model.diagrams?.length ? model.diagrams : [model];
		const elements = pages.reduce((count, page) => count + (page.elements?.length ?? 0), 0);
		const flows = pages.reduce((count, page) => count + (page.flows?.length ?? 0), 0);
		if (pages.length > 32 || elements > 1024 || flows > 2048 || elements * flows > 1000000) {
			throw new Error('Studio is limited to 32 pages, 1024 elements, 2048 flows and one million element/flow pairs.');
		}
	}

	private async publish(session: Session, except?: vscode.WebviewPanel): Promise<void> {
		const version = session.document.version;
		let model: unknown;
		let error: string | undefined;
		try {
			if (!vscode.workspace.isTrusted) throw new Error('Trust this workspace before editing models.');
			const text = session.document.getText();
			await this.preflight(session.engine, text);
			const parsed = JSON.parse(text);
			model = { ...parsed, elements: parsed.elements ?? [], flows: parsed.flows ?? [] };
		} catch (failure) { error = failure instanceof Error ? failure.message : String(failure); }
		if (session.document.isClosed || session.document.version !== version) return;
		for (const panel of session.panels) {
			if (panel !== except) void panel.webview.postMessage({ type: 'document', ...this.documentStatus(session.document), model, error });
		}
	}

	async edit(document: vscode.TextDocument, version: number, previous: unknown, next: unknown, origin?: vscode.WebviewPanel): Promise<{ version: number; dirty: boolean }> {
		const session = this.session(document);
		const operation = session.queue.then(async () => {
			if (!vscode.workspace.isTrusted) throw new Error('Trust this workspace before editing models.');
			if (!document.uri.path.toLowerCase().endsWith('.tmforge.json')) throw new Error('Studio edits only .tmforge.json documents.');
			const ensureCurrent = () => {
				if (document.isClosed || version !== document.version) throw new Error('The document changed in another editor. Reloaded the current source; retry your edit.');
			};
			ensureCurrent();
			const text = document.getText();
			const updated = applyModelChange(text, previous, next);
			await this.preflight(session.engine, updated);
			ensureCurrent();
			if (text !== updated) {
				const change = textChange(text, updated);
				const edits = new vscode.WorkspaceEdit();
				edits.replace(document.uri, new vscode.Range(document.positionAt(change.start), document.positionAt(change.end)), change.text);
				session.origin = origin;
				try {
					if (!await vscode.workspace.applyEdit(edits)) throw new Error('VS Code could not apply the model edit.');
				} finally { session.origin = undefined; }
			}
			return { version: document.version, dirty: document.isDirty };
		});
		session.queue = operation.then(() => {}, () => {});
		return operation;
	}

	async create(model: unknown = { schema: 'tmforge-json', version: '0.1', elements: [], flows: [] }, name = 'model.tmforge.json'): Promise<void> {
		const safeName = basename(name).replace(/[^a-zA-Z0-9._-]/g, '-').replace(/\.tmforge\.json$/i, '');
		const uri = vscode.Uri.from({ scheme: 'untitled', path: `${safeName}-${randomUUID().slice(0, 8)}.tmforge.json` });
		const document = await vscode.workspace.openTextDocument(uri);
		const edits = new vscode.WorkspaceEdit();
		edits.insert(uri, new vscode.Position(0, 0), JSON.stringify(model, null, 2) + '\n');
		await vscode.workspace.applyEdit(edits);
		await vscode.languages.setTextDocumentLanguage(document, 'json');
		await vscode.commands.executeCommand('vscode.openWith', uri, 'tmforge.studio');
	}

	private async receive(session: Session, panel: vscode.WebviewPanel, message: Record<string, unknown>): Promise<void> {
		if (message.type !== 'request' || !Number.isSafeInteger(message.id) || typeof message.method !== 'string') return;
		try {
			if (!vscode.workspace.isTrusted) throw new Error('Trust this workspace before editing models.');
			if (Buffer.byteLength(JSON.stringify(message), 'utf8') > MAX_REQUEST_BYTES) throw new Error('Studio requests are limited to 16 MiB.');
			const result = await this.action(session, panel, message.method, message.params as Record<string, unknown> ?? {});
			void panel.webview.postMessage({ type: 'response', id: message.id, result });
		} catch (failure) {
			void panel.webview.postMessage({ type: 'response', id: message.id, error: failure instanceof Error ? failure.message : String(failure) });
			if (message.method === 'edit') void this.publish(session);
		}
	}

	private async action(session: Session, panel: vscode.WebviewPanel, method: string, params: Record<string, unknown>): Promise<unknown> {
		const document = session.document;
		switch (method) {
			case 'engine': {
				if (typeof params.method !== 'string' || !Array.isArray(params.args)) throw new Error('Invalid engine request.');
				const result = await session.engine.invoke(params.method, params.args as string[]);
				if (params.method === 'SetRules') this.reanalyze(document);
				return result;
			}
			case 'edit': return this.edit(document, params.version as number, params.previous, params.next, panel);
			case 'save':
				await session.queue;
				if (!await document.save()) throw new Error('The document was not saved.');
				return this.documentStatus(document);
			case 'undo': case 'redo':
				await session.queue;
				await vscode.commands.executeCommand(method);
				return null;
			case 'source': await vscode.commands.executeCommand('vscode.openWith', document.uri, 'default'); return null;
			case 'theme': await vscode.commands.executeCommand('workbench.action.selectTheme'); return null;
			case 'confirm':
				if (typeof params.message !== 'string') throw new Error('Invalid confirmation.');
				return await vscode.window.showWarningMessage(params.message.slice(0, 1000), { modal: true }, 'Delete') === 'Delete';
			case 'open': {
				const picked = await vscode.window.showOpenDialog({ canSelectMany: false, openLabel: 'Open Model' });
				if (!picked?.length) return null;
				if (picked[0].path.toLowerCase().endsWith('.tmforge.json')) {
					await vscode.commands.executeCommand('vscode.openWith', picked[0], 'tmforge.studio');
					return null;
				}
				if ((await vscode.workspace.fs.stat(picked[0])).size > MAX_DOCUMENT_BYTES) throw new Error('Model imports are limited to 8 MiB.');
				return { name: basename(picked[0].path), content: Buffer.from(await vscode.workspace.fs.readFile(picked[0])).toString('base64') };
			}
			case 'create':
				await this.preflight(session.engine, JSON.stringify(params.model));
				await this.create(params.model, typeof params.name === 'string' ? params.name : undefined);
				return null;
			case 'download': {
				if (typeof params.content !== 'string' || typeof params.name !== 'string') throw new Error('Invalid export.');
				const content = Buffer.from(params.content, 'base64');
				const target = await vscode.window.showSaveDialog({ defaultUri: vscode.Uri.joinPath(document.uri.with({ scheme: document.isUntitled ? 'file' : document.uri.scheme }), '..', basename(params.name)), saveLabel: 'Export' });
				if (!target) return null;
				if (target.toString() === document.uri.toString()) throw new Error('Use Save to update the open model, or export to a separate file.');
				await vscode.workspace.fs.writeFile(target, content);
				return null;
			}
			default: throw new Error('Unsupported Studio action.');
		}
	}

	dispose(): void {
		for (const subscription of this.subscriptions) subscription.dispose();
		for (const session of this.sessions.values()) session.engine.dispose();
		this.sessions.clear();
	}
}
