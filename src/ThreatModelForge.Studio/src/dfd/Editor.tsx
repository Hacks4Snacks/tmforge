import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import {
  ReactFlow,
  Background,
  BackgroundVariant,
  Controls,
  MiniMap,
  Panel,
  addEdge,
  reconnectEdge,
  applyEdgeChanges as applyEdgeChangesToGraph,
  useNodesState,
  useEdgesState,
  useReactFlow,
  ConnectionMode,
  MarkerType,
  type Connection,
  type NodeTypes,
  type EdgeTypes,
  type DefaultEdgeOptions,
  type EdgeChange,
  type XYPosition,
} from '@xyflow/react';
import '@xyflow/react/dist/style.css';
import type { DragEvent as ReactDragEvent } from 'react';
import { ShapeNode } from './nodes/ShapeNode';
import { TrustBoundaryNode } from './nodes/TrustBoundaryNode';
import { Palette } from './Palette';
import { Toolbar } from './Toolbar';
import { Inspector } from './Inspector';
import { AnalysisSettings } from './AnalysisSettings';
import { FALLBACK_PACKS, FALLBACK_STENCILS } from './stencils';
import { createHttpEngine, loadWasmEngine, offlineEngine, probeEngine, type Finding, type FormatInfo, type IEngineClient, type PackInfo, type PropertyDescriptorInfo, type RuleInfo, type RulePackInfo, type StencilInfo, type Threat } from './engineClient';
import { ThreatsPanel, type NewThreatDraft, type ThreatEdit, type ThreatScopeOption } from './ThreatsPanel';
import { CanvasSearch, type SearchItem } from './CanvasSearch';
import { DEFAULT_NODE_SIZE, modelFromPages, pagesFromModel, type PageGraph } from './mapping';
import { tidyGraph } from './autosize';
import { cloneGraph, type Clipboard } from './clipboard';
import { useUndoRedo } from './useUndoRedo';
import { FlowEdge } from './edges/FlowEdge';
import { PageTabs } from './PageTabs';
import { MergeResolveModal } from './MergeResolveModal';
import { DfdActionsContext, type DfdActions } from './editorContext';
import { Toaster, toast } from './toast';
import type { DfdEdge, DfdKind, DfdNode, ThreatTriage, TmForgeModel, TmForgeAnalysis } from './types';

const nodeTypes: NodeTypes = {
  process: ShapeNode,
  datastore: ShapeNode,
  external: ShapeNode,
  boundary: TrustBoundaryNode,
};

const edgeTypes: EdgeTypes = {
  flow: FlowEdge,
};

const defaultEdgeOptions: DefaultEdgeOptions = {
  type: 'flow',
  markerEnd: { type: MarkerType.ArrowClosed },
};

const KIND_COLOR: Record<DfdKind, string> = {
  process: '#6366f1',
  datastore: '#0d9488',
  external: '#475569',
  boundary: '#64748b',
};

export const STORAGE_KEY = 'tmforge.studio.workspace.v2';
export const LEGACY_MODEL_KEY = 'tmforge.studio.model.v1';
export const THEME_KEY = 'tmforge.studio.theme';
export const RECENTS_KEY = 'tmforge.studio.recentStencils.v1';
/** How many recently used stencils to remember. */
export const RECENTS_MAX = 6;
/** Canvas grid pitch (px). Shared by the dotted background and snap-to-grid so they line up. */
const GRID_SIZE = 16;
/** Offset (px) applied to pasted/duplicated elements so a copy is visibly distinct from its source. */
const PASTE_OFFSET = { x: GRID_SIZE * 2, y: GRID_SIZE * 2 };
const PACKS_DISABLED_KEY = 'tmforge.studio.disabledPacks.v1';
const FAVORITES_KEY = 'tmforge.studio.favoriteStencils.v1';

type Theme = 'light' | 'dark';

/** The initial theme: a saved choice if present, else the OS preference. */
export function initialTheme(): Theme {
  try {
    const saved = window.localStorage.getItem(THEME_KEY);
    if (saved === 'light' || saved === 'dark') {
      return saved;
    }
  } catch {
    /* storage unavailable */
  }
  return window.matchMedia?.('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
}

interface StoredWorkspace {
  pages: PageGraph[];
  activePageId: string;
  analysis?: TmForgeAnalysis;
  threats?: ThreatTriage[];
}

/** Reads the saved multi-page workspace (v2), migrating a legacy single-page model (v1) when present. */
export function loadStoredWorkspace(): StoredWorkspace | null {
  try {
    const raw = window.localStorage.getItem(STORAGE_KEY);
    if (raw) {
      const parsed = JSON.parse(raw) as { model?: TmForgeModel; activePageId?: string };
      if (parsed?.model?.schema === 'tmforge-json') {
        const pages = pagesFromModel(parsed.model);
        const activePageId = pages.some((p) => p.id === parsed.activePageId) ? parsed.activePageId! : pages[0].id;
        return { pages, activePageId, analysis: parsed.model.analysis, threats: parsed.model.threats };
      }
    }
  } catch {
    /* fall through to legacy / empty */
  }
  try {
    const raw = window.localStorage.getItem(LEGACY_MODEL_KEY);
    if (raw) {
      const model = JSON.parse(raw) as TmForgeModel;
      if (model?.schema === 'tmforge-json') {
        const pages = pagesFromModel(model);
        return { pages, activePageId: pages[0].id, analysis: model.analysis, threats: model.threats };
      }
    }
  } catch {
    /* fall through to empty */
  }
  return null;
}

/** A fresh workspace with a single empty page. */
export function emptyWorkspace(): StoredWorkspace {
  const id = crypto.randomUUID();
  return { pages: [{ id, name: 'Page 1', nodes: [], edges: [] }], activePageId: id };
}

/** Reads the recently used stencil ids from browser storage (most recent first). */
export function loadRecentStencilIds(): string[] {
  try {
    const raw = window.localStorage.getItem(RECENTS_KEY);
    if (!raw) {
      return [];
    }
    const parsed = JSON.parse(raw) as unknown;
    return Array.isArray(parsed)
      ? parsed.filter((x): x is string => typeof x === 'string').slice(0, RECENTS_MAX)
      : [];
  } catch {
    return [];
  }
}

/** Reads a persisted list of string ids from browser storage. */
export function loadStringList(key: string): string[] {
  try {
    const raw = window.localStorage.getItem(key);
    if (!raw) {
      return [];
    }
    const parsed = JSON.parse(raw) as unknown;
    return Array.isArray(parsed) ? parsed.filter((x): x is string => typeof x === 'string') : [];
  } catch {
    return [];
  }
}

/** Persists a list of string ids to browser storage, ignoring storage failures. */
export function persistStringList(key: string, value: string[]): void {
  try {
    window.localStorage.setItem(key, JSON.stringify(value));
  } catch {
    /* storage unavailable */
  }
}

// Restore the last saved workspace on load, else start from a single empty page.
const INITIAL_WORKSPACE = loadStoredWorkspace() ?? emptyWorkspace();
const INITIAL_ACTIVE =
  INITIAL_WORKSPACE.pages.find((p) => p.id === INITIAL_WORKSPACE.activePageId) ?? INITIAL_WORKSPACE.pages[0];
const INITIAL_DISABLED_PACKS = INITIAL_WORKSPACE.analysis?.disabledPacks ?? [];
const INITIAL_DISABLED_RULE_IDS = INITIAL_WORKSPACE.analysis?.disabledRuleIds ?? [];
const INITIAL_SAVED_JSON = JSON.stringify(
  modelFromPages(INITIAL_WORKSPACE.pages, buildAnalysis(INITIAL_DISABLED_PACKS, INITIAL_DISABLED_RULE_IDS)),
);

/** Minimal shape of the File System Access API used to open and overwrite files (Chromium). */
interface WritableFileHandle {
  readonly name: string;
  getFile(): Promise<File>;
  createWritable(): Promise<{ write(data: BlobPart): Promise<void>; close(): Promise<void> }>;
}

interface FilePickerWindow {
  showOpenFilePicker?: () => Promise<WritableFileHandle[]>;
  showSaveFilePicker?: (options?: { suggestedName?: string }) => Promise<WritableFileHandle>;
}

/** True when a file-picker promise rejected because the user cancelled the dialog. */
export function isAbortError(err: unknown): boolean {
  return err instanceof DOMException && err.name === 'AbortError';
}

function downloadBlob(blob: Blob, filename: string): void {
  const url = URL.createObjectURL(blob);
  const anchor = document.createElement('a');
  anchor.href = url;
  anchor.download = filename;
  anchor.click();
  URL.revokeObjectURL(url);
}

/** Builds the analysis-rule selection from the current toggles, or undefined when nothing is disabled. */
export function buildAnalysis(disabledPacks: string[], disabledRuleIds: string[]): TmForgeAnalysis | undefined {
  const analysis: TmForgeAnalysis = {};
  if (disabledPacks.length > 0) {
    analysis.disabledPacks = disabledPacks;
  }
  if (disabledRuleIds.length > 0) {
    analysis.disabledRuleIds = disabledRuleIds;
  }
  return analysis.disabledPacks || analysis.disabledRuleIds ? analysis : undefined;
}

/** Returns copies of the graph with the `flagged` class applied to elements a finding referenced. */
export function applyFlags(
  nodes: DfdNode[],
  edges: DfdEdge[],
  flagged: ReadonlySet<string>,
): { nodes: DfdNode[]; edges: DfdEdge[] } {
  return {
    nodes: nodes.map((n) => ({ ...n, className: flagged.has(n.id) ? 'flagged' : undefined })),
    edges: edges.map((e) => ({ ...e, className: flagged.has(e.id) ? 'flagged' : undefined })),
  };
}

/**
 * Applies React Flow edge changes without letting controlled-prop synchronization discard Tidy's
 * geometry. React Flow can emit `replace` copies after an unrelated canvas edit; those copies do
 * not carry Studio-only handles, routes, or label offsets. Preserve that state when the endpoints
 * are unchanged. A real reconnect changes an endpoint and intentionally receives fresh geometry.
 */
export function applyCanvasEdgeChanges(changes: EdgeChange<DfdEdge>[], current: DfdEdge[]): DfdEdge[] {
  const byId = new Map(current.map((edge) => [edge.id, edge]));
  const geometrySafe = changes.map((change): EdgeChange<DfdEdge> => {
    if (change.type !== 'replace') {
      return change;
    }
    const existing = byId.get(change.id);
    const replacement = change.item;
    if (!existing || existing.source !== replacement.source || existing.target !== replacement.target) {
      return change;
    }
    return {
      ...change,
      item: {
        ...replacement,
        sourceHandle: replacement.sourceHandle ?? existing.sourceHandle,
        targetHandle: replacement.targetHandle ?? existing.targetHandle,
        data:
          existing.data || replacement.data
            ? { ...existing.data, ...replacement.data }
            : undefined,
      },
    };
  });
  return applyEdgeChangesToGraph(geometrySafe, current);
}

export function Editor() {
  const [pages, setPages] = useState<PageGraph[]>(INITIAL_WORKSPACE.pages);
  const [activePageId, setActivePageId] = useState<string>(INITIAL_ACTIVE.id);
  const [nodes, setNodes, onNodesChange] = useNodesState<DfdNode>(INITIAL_ACTIVE.nodes);
  const [edges, setEdges] = useEdgesState<DfdEdge>(INITIAL_ACTIVE.edges);
  const [findings, setFindings] = useState<Finding[]>([]);
  const [threats, setThreats] = useState<Threat[]>([]);
  const [threatTriage, setThreatTriage] = useState<ThreatTriage[]>(INITIAL_WORKSPACE.threats ?? []);
  const flaggedIdsRef = useRef<ReadonlySet<string>>(new Set());
  const [engine, setEngine] = useState<IEngineClient>(offlineEngine);
  const [engineOnline, setEngineOnline] = useState(false);
  const [formats, setFormats] = useState<FormatInfo[]>([]);
  const [selection, setSelection] = useState<{ node: string | null; edge: string | null }>({ node: null, edge: null });
  const [stencils, setStencils] = useState<StencilInfo[]>(FALLBACK_STENCILS);
  const [recentStencilIds, setRecentStencilIds] = useState<string[]>(loadRecentStencilIds);
  const [packs, setPacks] = useState<PackInfo[]>(FALLBACK_PACKS);
  const [disabledPacks, setDisabledPacks] = useState<string[]>(() => loadStringList(PACKS_DISABLED_KEY));
  const [favoriteIds, setFavoriteIds] = useState<string[]>(() => loadStringList(FAVORITES_KEY));
  const [rules, setRules] = useState<RuleInfo[]>([]);
  const [rulePacks, setRulePacks] = useState<RulePackInfo[]>([]);
  const [disabledRulePacks, setDisabledRulePacks] = useState<string[]>(() => INITIAL_DISABLED_PACKS);
  const [disabledRuleIds, setDisabledRuleIds] = useState<string[]>(() => INITIAL_DISABLED_RULE_IDS);
  const [showRules, setShowRules] = useState(false);
  const [showMerge, setShowMerge] = useState(false);
  const analysisActiveRef = useRef(false);
  const fileRef = useRef<HTMLInputElement>(null);
  const fileHandleRef = useRef<WritableFileHandle | null>(null);
  const fileFormatRef = useRef<string>('tmforge-json');
  const [fileName, setFileName] = useState<string | null>(null);
  const { screenToFlowPosition, fitView } = useReactFlow();
  const { takeSnapshot, undo, redo, canUndo, canRedo, reset } = useUndoRedo(nodes, edges, setNodes, setEdges);

  const [theme, setTheme] = useState<Theme>(initialTheme);
  useEffect(() => {
    document.documentElement.setAttribute('data-theme', theme);
    try {
      window.localStorage.setItem(THEME_KEY, theme);
    } catch {
      /* storage unavailable */
    }
  }, [theme]);
  const toggleTheme = useCallback(() => setTheme((t) => (t === 'dark' ? 'light' : 'dark')), []);

  useEffect(() => {
    let active = true;
    void (async () => {
      // 1. Prefer the hosted /v1 engine — unless this is a static demo build with no /v1 to reach.
      if (import.meta.env.VITE_DEMO !== 'true' && (await probeEngine())) {
        if (active) {
          setEngine(createHttpEngine());
          setEngineOnline(true);
        }
        return;
      }
      // 2. Fall back to the in-browser WebAssembly engine — the SAME engine, no backend.
      const wasm = await loadWasmEngine();
      if (active && wasm) {
        setEngine(wasm);
        setEngineOnline(true);
        return;
      }
      // 3. Neither reachable: the built-in offline client (client-side authoring) remains.
    })();
    return () => {
      active = false;
    };
  }, []);

  useEffect(() => {
    let active = true;
    void engine
      .getFormats()
      .then((list) => {
        if (active) {
          setFormats(list);
        }
      })
      .catch(() => {
        if (active) {
          setFormats([]);
        }
      });
    return () => {
      active = false;
    };
  }, [engine]);

  // Load the stencil catalog from whichever engine is active (the real /v1/stencils when online,
  // the generic fallback offline).
  useEffect(() => {
    let active = true;
    void engine
      .getStencils()
      .then((list) => {
        if (active && list.length) {
          setStencils(list);
        }
      })
      .catch(() => {
        /* keep the fallback catalog */
      });
    return () => {
      active = false;
    };
  }, [engine]);

  // Load the stencil packs (for the palette's show/hide toggles) from the active engine.
  useEffect(() => {
    let active = true;
    void engine
      .getStencilPacks()
      .then((list) => {
        if (active && list.length) {
          setPacks(list);
        }
      })
      .catch(() => {
        /* keep the fallback packs */
      });
    return () => {
      active = false;
    };
  }, [engine]);

  // Load the typed property schema (drives the Inspector's typed controls) from the active engine.
  const [propertySchema, setPropertySchema] = useState<PropertyDescriptorInfo[]>([]);
  useEffect(() => {
    let active = true;
    void engine
      .getPropertySchema()
      .then((list) => {
        if (active && list.length) {
          setPropertySchema(list);
        }
      })
      .catch(() => {
        /* no typed schema offline; the Inspector falls back to free text */
      });
    return () => {
      active = false;
    };
  }, [engine]);

  const stencilById = useMemo(() => new Map(stencils.map((s) => [s.id, s])), [stencils]);

  // Load the analysis rule catalog + rule packs (for the Analysis Rules settings panel) from the engine.
  useEffect(() => {
    let active = true;
    void engine
      .getRules()
      .then((list) => {
        if (active) {
          setRules(list);
        }
      })
      .catch(() => {
        if (active) {
          setRules([]);
        }
      });
    return () => {
      active = false;
    };
  }, [engine]);

  useEffect(() => {
    let active = true;
    void engine
      .getRulePacks()
      .then((list) => {
        if (active) {
          setRulePacks(list);
        }
      })
      .catch(() => {
        if (active) {
          setRulePacks([]);
        }
      });
    return () => {
      active = false;
    };
  }, [engine]);

  // All pages, with the live React Flow graph substituted for the active page (the store's copy of
  // the active page is only refreshed on switch / page op, so composed reads use the live graph).
  const allPages = useMemo<PageGraph[]>(
    () => pages.map((p) => (p.id === activePageId ? { ...p, nodes, edges } : p)),
    [pages, activePageId, nodes, edges],
  );

  // Persistence. `currentJson` serializes the model the way Save writes it (selection is not part of
  // the model, so selecting a node never marks it dirty); `dirty` compares it to the snapshot from
  // the last explicit Save. A debounced localStorage write of the whole workspace (pages + active
  // tab) runs on every change as a crash-recovery net, so a reload never loses work.
  const currentModel = useMemo(() => {
    const model = modelFromPages(allPages, buildAnalysis(disabledRulePacks, disabledRuleIds));
    return threatTriage.length > 0 ? { ...model, threats: threatTriage } : model;
  }, [allPages, disabledRulePacks, disabledRuleIds, threatTriage]);
  const currentJson = useMemo(() => JSON.stringify(currentModel), [currentModel]);
  const [savedJson, setSavedJson] = useState(INITIAL_SAVED_JSON);
  const dirty = currentJson !== savedJson;

  const workspaceJson = useMemo(
    () => JSON.stringify({ v: 2, activePageId, model: currentModel }),
    [activePageId, currentModel],
  );
  useEffect(() => {
    const id = window.setTimeout(() => {
      try {
        window.localStorage.setItem(STORAGE_KEY, workspaceJson);
      } catch {
        /* storage unavailable (private mode / quota) — ignore */
      }
    }, 600);
    return () => window.clearTimeout(id);
  }, [workspaceJson]);

  // ---- pages: switch, add, rename, delete, reorder ----
  const switchPage = useCallback(
    (targetId: string) => {
      if (targetId === activePageId) {
        return;
      }
      const committed = allPages;
      const target = committed.find((p) => p.id === targetId);
      if (!target) {
        return;
      }
      setPages(committed);
      setActivePageId(targetId);
      const applied = analysisActiveRef.current
        ? applyFlags(target.nodes, target.edges, flaggedIdsRef.current)
        : { nodes: target.nodes, edges: target.edges };
      setNodes(applied.nodes);
      setEdges(applied.edges);
      setSelection({ node: null, edge: null });
      reset();
      window.setTimeout(() => fitView({ padding: 0.25, maxZoom: 1.15, duration: 200 }), 0);
    },
    [allPages, activePageId, setNodes, setEdges, reset, fitView],
  );

  const addPage = useCallback(() => {
    const id = crypto.randomUUID();
    setPages([...allPages, { id, name: `Page ${pages.length + 1}`, nodes: [], edges: [] }]);
    setActivePageId(id);
    setNodes([]);
    setEdges([]);
    setSelection({ node: null, edge: null });
    reset();
  }, [allPages, pages.length, setNodes, setEdges, reset]);

  const renamePage = useCallback((id: string, name: string) => {
    setPages((prev) => prev.map((p) => (p.id === id ? { ...p, name } : p)));
  }, []);

  const deletePage = useCallback(
    (id: string) => {
      if (pages.length <= 1) {
        return;
      }
      const committed = allPages;
      const victim = committed.find((p) => p.id === id);
      if (
        victim &&
        (victim.nodes.length > 0 || victim.edges.length > 0) &&
        !window.confirm(`Delete page “${victim.name}” and its contents?`)
      ) {
        return;
      }
      const index = committed.findIndex((p) => p.id === id);
      const remaining = committed.filter((p) => p.id !== id);
      setPages(remaining);
      if (id === activePageId) {
        const next = remaining[Math.min(index, remaining.length - 1)];
        setActivePageId(next.id);
        const applied = analysisActiveRef.current
          ? applyFlags(next.nodes, next.edges, flaggedIdsRef.current)
          : { nodes: next.nodes, edges: next.edges };
        setNodes(applied.nodes);
        setEdges(applied.edges);
        setSelection({ node: null, edge: null });
        reset();
      }
    },
    [allPages, pages.length, activePageId, setNodes, setEdges, reset],
  );

  const reorderPage = useCallback(
    (from: number, to: number) => {
      const committed = allPages;
      if (from < 0 || to < 0 || from >= committed.length || to >= committed.length) {
        return;
      }
      const next = [...committed];
      const [moved] = next.splice(from, 1);
      next.splice(to, 0, moved);
      setPages(next);
    },
    [allPages],
  );

  // Which page each element id lives on, and which pages currently carry a finding (for tab badges).
  const elementPageIndex = useMemo(() => {
    const map = new Map<string, string>();
    for (const page of allPages) {
      for (const n of page.nodes) {
        map.set(n.id, page.id);
      }
      for (const e of page.edges) {
        map.set(e.id, page.id);
      }
    }
    return map;
  }, [allPages]);

  const findingPageIds = useMemo(() => {
    const set = new Set<string>();
    const elementIdLists = [...findings.map((f) => f.elementIds), ...threats.map((t) => t.elementIds)];
    for (const elementIds of elementIdLists) {
      for (const id of elementIds) {
        const pageId = elementPageIndex.get(id);
        if (pageId) {
          set.add(pageId);
        }
      }
    }
    return set;
  }, [findings, threats, elementPageIndex]);

  const serializeModel = useCallback(
    async (formatId: string): Promise<Blob> => {
      if (formatId === 'tmforge-json') {
        return new Blob([await engine.write(currentModel)], { type: 'application/json' });
      }
      return engine.convert(currentModel, formatId);
    },
    [engine, currentModel],
  );

  const writeToHandle = useCallback(
    async (handle: WritableFileHandle, formatId: string): Promise<void> => {
      const blob = await serializeModel(formatId);
      const writable = await handle.createWritable();
      await writable.write(blob);
      await writable.close();
    },
    [serializeModel],
  );

  // Save As: pick a new file and write it (Chromium), else download a copy (other browsers).
  const saveAs = useCallback(async () => {
    const picker = window as unknown as FilePickerWindow;
    const formatId = fileFormatRef.current;
    const ext = formats.find((f) => f.id === formatId)?.extensions[0] ?? '.tmforge.json';
    const suggestedName = fileName ?? `model${ext}`;
    try {
      if (picker.showSaveFilePicker) {
        const handle = await picker.showSaveFilePicker({ suggestedName });
        await writeToHandle(handle, formatId);
        fileHandleRef.current = handle;
        setFileName(handle.name);
      } else {
        downloadBlob(await serializeModel(formatId), suggestedName);
      }
      setSavedJson(currentJson);
    } catch (err) {
      if (!isAbortError(err)) {
        toast(err instanceof Error ? err.message : 'Could not save the file.', 'error');
      }
    }
  }, [currentJson, fileName, formats, serializeModel, writeToHandle]);

  // Save: silently overwrite the file this model was opened from / last saved to; when no file is
  // bound (or the browser lacks the File System Access API) fall back to Save As.
  const saveModel = useCallback(async () => {
    const handle = fileHandleRef.current;
    if (!handle) {
      await saveAs();
      return;
    }
    try {
      await writeToHandle(handle, fileFormatRef.current);
      setSavedJson(currentJson);
    } catch (err) {
      toast(err instanceof Error ? err.message : 'Could not write the file.', 'error');
    }
  }, [currentJson, saveAs, writeToHandle]);

  // Keep a ref so the global keydown handler always calls the latest saveModel without re-subscribing.
  const saveModelRef = useRef(saveModel);
  saveModelRef.current = saveModel;

  // ---- clipboard: copy / paste / duplicate the selected nodes and the flows between them ----
  const clipboardRef = useRef<Clipboard | null>(null);

  const copySelection = useCallback(() => {
    const selectedNodes = nodes.filter((n) => n.selected);
    const selectedEdges = edges.filter((e) => e.selected);
    if (selectedNodes.length === 0 && selectedEdges.length === 0) {
      return;
    }
    clipboardRef.current = { nodes: selectedNodes, edges: selectedEdges };
  }, [nodes, edges]);

  // Drops a freshly-cloned graph onto the canvas: existing elements are deselected and the clones
  // become the selection, so the copy is ready to nudge and a repeat paste offsets from this one.
  const placeClone = useCallback(
    (clone: Clipboard) => {
      if (clone.nodes.length === 0 && clone.edges.length === 0) {
        return;
      }
      takeSnapshot();
      setNodes((nds) => [...nds.map((n) => (n.selected ? { ...n, selected: false } : n)), ...clone.nodes]);
      setEdges((eds) => [...eds.map((e) => (e.selected ? { ...e, selected: false } : e)), ...clone.edges]);
      setSelection({
        node: clone.nodes[0]?.id ?? null,
        edge: clone.nodes.length === 0 ? clone.edges[0]?.id ?? null : null,
      });
    },
    [setNodes, setEdges, takeSnapshot],
  );

  const pasteClipboard = useCallback(() => {
    const clip = clipboardRef.current;
    if (clip) {
      placeClone(cloneGraph(clip.nodes, clip.edges, PASTE_OFFSET));
    }
  }, [placeClone]);

  const duplicateSelection = useCallback(() => {
    const selectedNodes = nodes.filter((n) => n.selected);
    const selectedEdges = edges.filter((e) => e.selected);
    placeClone(cloneGraph(selectedNodes, selectedEdges, PASTE_OFFSET));
  }, [nodes, edges, placeClone]);

  // Latest clipboard actions for the global keydown handler, so it never re-subscribes on every edit.
  const clipboardActionsRef = useRef({ copy: copySelection, paste: pasteClipboard, duplicate: duplicateSelection });
  clipboardActionsRef.current = { copy: copySelection, paste: pasteClipboard, duplicate: duplicateSelection };

  useEffect(() => {
    const onKey = (event: KeyboardEvent) => {
      // Cmd/Ctrl+S saves — even while typing in a field — and never opens the browser's save dialog.
      if ((event.metaKey || event.ctrlKey) && (event.key === 's' || event.key === 'S')) {
        event.preventDefault();
        saveModelRef.current();
        return;
      }
      const target = event.target as HTMLElement | null;
      const tag = target?.tagName;
      if (tag === 'INPUT' || tag === 'TEXTAREA' || tag === 'SELECT' || target?.isContentEditable) {
        return;
      }
      if (!(event.metaKey || event.ctrlKey)) {
        return;
      }
      if (event.key === 'z' || event.key === 'Z') {
        event.preventDefault();
        if (event.shiftKey) {
          redo();
        } else {
          undo();
        }
      } else if (event.key === 'y') {
        event.preventDefault();
        redo();
      } else if (event.key === 'c' || event.key === 'C') {
        clipboardActionsRef.current.copy();
      } else if (event.key === 'v' || event.key === 'V') {
        event.preventDefault();
        clipboardActionsRef.current.paste();
      } else if (event.key === 'd' || event.key === 'D') {
        event.preventDefault();
        clipboardActionsRef.current.duplicate();
      }
    };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, [undo, redo]);

  const onConnect = useCallback(
    (connection: Connection) => {
      takeSnapshot();
      setEdges((eds) =>
        addEdge(
          {
            ...connection,
            type: 'flow',
            markerEnd: { type: MarkerType.ArrowClosed },
            label: 'data flow',
            data: { properties: {} },
          },
          eds,
        ),
      );
    },
    [setEdges, takeSnapshot],
  );

  // Drag either end of an existing flow onto a different node or port to re-pin it, instead of
  // deleting the connection and drawing a new one.
  const onReconnect = useCallback(
    (oldEdge: DfdEdge, newConnection: Connection) => {
      takeSnapshot();
      setEdges((eds) => reconnectEdge(oldEdge, newConnection, eds));
    },
    [setEdges, takeSnapshot],
  );

  const onEdgesChange = useCallback(
    (changes: EdgeChange<DfdEdge>[]) => {
      setEdges((current) => applyCanvasEdgeChanges(changes, current));
    },
    [setEdges],
  );

  // Show/hide a whole stencil pack in the palette (persisted).
  const togglePack = useCallback((packId: string) => {
    setDisabledPacks((prev) => {
      const next = prev.includes(packId) ? prev.filter((x) => x !== packId) : [...prev, packId];
      persistStringList(PACKS_DISABLED_KEY, next);
      return next;
    });
  }, []);

  // Star/unstar a stencil so it appears in the palette's Favorites row (persisted).
  const toggleFavorite = useCallback((stencilId: string) => {
    setFavoriteIds((prev) => {
      const next = prev.includes(stencilId) ? prev.filter((x) => x !== stencilId) : [stencilId, ...prev];
      persistStringList(FAVORITES_KEY, next);
      return next;
    });
  }, []);

  // Enable/disable a whole rule pack for this model. The selection travels with the model (saved in
  // the file and sent on analyze), so it is model state, not a persisted UI preference.
  const toggleRulePack = useCallback((packId: string) => {
    setDisabledRulePacks((prev) =>
      prev.includes(packId) ? prev.filter((x) => x !== packId) : [...prev, packId],
    );
  }, []);

  // Enable/disable a single rule for this model.
  const toggleRule = useCallback((ruleId: string) => {
    setDisabledRuleIds((prev) =>
      prev.includes(ruleId) ? prev.filter((x) => x !== ruleId) : [...prev, ruleId],
    );
  }, []);

  // Remember the last few stencils the user placed, so the palette can surface them.
  const recordRecentStencil = useCallback((stencilId: string) => {
    setRecentStencilIds((prev) => {
      const next = [stencilId, ...prev.filter((x) => x !== stencilId)].slice(0, RECENTS_MAX);
      try {
        window.localStorage.setItem(RECENTS_KEY, JSON.stringify(next));
      } catch {
        /* storage unavailable */
      }
      return next;
    });
  }, []);

  const addNode = useCallback(
    (stencil: StencilInfo, position: XYPosition) => {
      const id = crypto.randomUUID();
      const base = stencil.base;
      // Only specialized stencils (id differs from the primitive) carry a StencilType identity.
      const stencilType = stencil.id === base ? undefined : stencil.id;
      const data = { label: stencil.label, stencilType, properties: { ...stencil.defaults } };
      const size = DEFAULT_NODE_SIZE[base];
      const node: DfdNode = {
        id,
        type: base,
        position,
        data,
        style: { width: size.width, height: size.height },
        zIndex: base === 'boundary' ? 0 : 1,
      };
      takeSnapshot();
      setNodes((nds) => nds.concat(node));
      recordRecentStencil(stencil.id);
    },
    [setNodes, takeSnapshot, recordRecentStencil],
  );

  const onDragOver = useCallback((event: ReactDragEvent) => {
    event.preventDefault();
    event.dataTransfer.dropEffect = 'move';
  }, []);

  const onDrop = useCallback(
    (event: ReactDragEvent) => {
      event.preventDefault();
      const stencilId = event.dataTransfer.getData('application/tmforge-stencil');
      const stencil = stencilById.get(stencilId);
      if (!stencil) {
        return;
      }
      addNode(stencil, screenToFlowPosition({ x: event.clientX, y: event.clientY }));
    },
    [addNode, screenToFlowPosition, stencilById],
  );

  const onSelectionChange = useCallback(
    ({ nodes: selectedNodes, edges: selectedEdges }: { nodes: DfdNode[]; edges: DfdEdge[] }) => {
      setSelection({ node: selectedNodes[0]?.id ?? null, edge: selectedEdges[0]?.id ?? null });
    },
    [],
  );

  const renameNode = useCallback(
    (id: string, label: string) =>
      setNodes((nds) => nds.map((n) => (n.id === id ? { ...n, data: { ...n.data, label } } : n))),
    [setNodes],
  );

  const renameEdge = useCallback(
    (id: string, label: string) => setEdges((eds) => eds.map((e) => (e.id === id ? { ...e, label } : e))),
    [setEdges],
  );

  // Persist a label's dragged offset (the snapshot is taken by the edge on drag start via beginEdit).
  const setEdgeLabelOffset = useCallback(
    (id: string, offset: { x: number; y: number }) =>
      setEdges((eds) =>
        eds.map((e) =>
          e.id === id ? { ...e, data: { ...e.data, labelOffset: offset, autoLabelOffset: false } } : e,
        ),
      ),
    [setEdges],
  );

  const setEdgeProperty = useCallback(
    (id: string, key: string, value: string) => {
      takeSnapshot();
      setEdges((eds) =>
        eds.map((e) => {
          if (e.id !== id) {
            return e;
          }
          const properties = { ...(e.data?.properties ?? {}) };
          if (value) {
            properties[key] = value;
          } else {
            delete properties[key];
          }
          return { ...e, data: { ...e.data, properties } };
        }),
      );
    },
    [setEdges, takeSnapshot],
  );

  const setNodeProperty = useCallback(
    (id: string, key: string, value: string) => {
      takeSnapshot();
      setNodes((nds) =>
        nds.map((n) => {
          if (n.id !== id) {
            return n;
          }
          const properties = { ...(n.data.properties ?? {}) };
          properties[key] = value;
          return { ...n, data: { ...n.data, properties } };
        }),
      );
    },
    [setNodes, takeSnapshot],
  );

  const removeNodeProperty = useCallback(
    (id: string, key: string) => {
      takeSnapshot();
      setNodes((nds) =>
        nds.map((n) => {
          if (n.id !== id) {
            return n;
          }
          const properties = { ...(n.data.properties ?? {}) };
          delete properties[key];
          return { ...n, data: { ...n.data, properties } };
        }),
      );
    },
    [setNodes, takeSnapshot],
  );

  const deleteSelected = useCallback(() => {
    if (!selection.node && !selection.edge) {
      return;
    }
    takeSnapshot();
    if (selection.node) {
      const nodeId = selection.node;
      setNodes((nds) => nds.filter((n) => n.id !== nodeId));
      setEdges((eds) => eds.filter((e) => e.source !== nodeId && e.target !== nodeId));
    } else if (selection.edge) {
      const edgeId = selection.edge;
      setEdges((eds) => eds.filter((e) => e.id !== edgeId));
    }
    setSelection({ node: null, edge: null });
  }, [selection, setNodes, setEdges, takeSnapshot]);

  const clearFlags = useCallback(() => {
    setNodes((nds) => nds.map((n) => (n.className ? { ...n, className: undefined } : n)));
    setEdges((eds) => eds.map((e) => (e.className ? { ...e, className: undefined } : e)));
    setFindings([]);
    setThreats([]);
    flaggedIdsRef.current = new Set();
    analysisActiveRef.current = false;
  }, [setNodes, setEdges]);

  // Analyze the model: generate the STRIDE threat register and, in parallel, the model-hygiene
  // findings. Threats (threat-bearing rules) and findings (the rest) are the same detection, so they
  // share one panel: the register leads, non-threat findings trail. The register carries the model's
  // acceptance triage, so accepted risks come back Accepted.
  const runAnalyze = useCallback(async () => {
    let generated: Threat[];
    let allFindings: Finding[];
    try {
      [generated, allFindings] = await Promise.all([
        engine.generateThreats(currentModel),
        engine.analyze(currentModel),
      ]);
    } catch (err) {
      toast(err instanceof Error ? err.message : 'Analysis failed.', 'error');
      return;
    }
    setThreats(generated);
    // "Other findings" = findings from non-threat-bearing (hygiene) rules; the threat-bearing ones
    // are already shown as threats.
    const threatRuleIds = new Set(generated.map((t) => t.ruleId));
    setFindings(allFindings.filter((f) => !f.ruleId || !threatRuleIds.has(f.ruleId)));
    analysisActiveRef.current = true;
    const flagged = new Set([...generated.flatMap((t) => t.elementIds), ...allFindings.flatMap((f) => f.elementIds)]);
    flaggedIdsRef.current = flagged;
    const applied = applyFlags(nodes, edges, flagged);
    setNodes(applied.nodes);
    setEdges(applied.edges);
  }, [engine, currentModel, nodes, edges, setNodes, setEdges]);

  // When an analysis is already on screen, changing the rule selection re-runs it so the results
  // reflect the new choice immediately. A ref holds the latest runAnalyze to avoid a dependency loop.
  const runAnalyzeRef = useRef(runAnalyze);
  runAnalyzeRef.current = runAnalyze;
  useEffect(() => {
    if (analysisActiveRef.current) {
      void runAnalyzeRef.current();
    }
  }, [disabledRulePacks, disabledRuleIds]);

  // Apply an author edit (state / priority / mitigation / description / justification) to a threat:
  // record it on the model's overlay (so it persists and round-trips) and reflect it in the panel.
  const editThreat = useCallback((threat: Threat, edit: ThreatEdit) => {
    setThreatTriage((prev) => {
      const existing = prev.find((t) => t.id === threat.id);
      const base: ThreatTriage = existing ?? {
        id: threat.id,
        state: 'Open',
        ...(threat.manual
          ? { manual: true, category: threat.category, title: threat.title, elementIds: threat.elementIds }
          : {}),
      };
      const next: ThreatTriage = { ...base, state: edit.state };
      if (edit.justification !== undefined) {
        next.justification = edit.justification;
      }
      if (edit.priority !== undefined) {
        next.priority = edit.priority;
      }
      if (edit.description !== undefined) {
        next.description = edit.description;
      }
      if (edit.mitigation !== undefined) {
        next.mitigation = edit.mitigation;
      }
      return [...prev.filter((t) => t.id !== threat.id), next];
    });
    setThreats((prev) =>
      prev.map((t) =>
        t.id === threat.id
          ? {
              ...t,
              state: edit.state,
              justification: edit.justification ?? t.justification,
              priority: edit.priority ?? t.priority,
              description: edit.description ?? t.description,
              mitigation: edit.mitigation ?? t.mitigation,
            }
          : t,
      ),
    );
  }, []);

  // Delete a manually-authored threat from the overlay and the panel.
  const deleteThreat = useCallback((threat: Threat) => {
    setThreatTriage((prev) => prev.filter((t) => t.id !== threat.id));
    setThreats((prev) => prev.filter((t) => t.id !== threat.id));
  }, []);

  // Navigates to the page holding the first of a threat's / finding's referenced elements.
  const jumpToElements = useCallback(
    (elementIds: string[]) => {
      for (const id of elementIds) {
        const pageId = elementPageIndex.get(id);
        if (pageId && pageId !== activePageId) {
          switchPage(pageId);
          return;
        }
      }
    },
    [elementPageIndex, activePageId, switchPage],
  );

  // The name of the page a threat's elements live on, when that is not the page in view (for a badge).
  const offPageLabel = useCallback(
    (elementIds: string[]): string | undefined => {
      const pageId = elementIds.map((id) => elementPageIndex.get(id)).find(Boolean);
      if (!pageId || pageId === activePageId) {
        return undefined;
      }
      return pages.find((p) => p.id === pageId)?.name;
    },
    [elementPageIndex, activePageId, pages],
  );

  // Every placed element and flow across all pages, for the canvas search box.
  const searchItems = useMemo<SearchItem[]>(() => {
    const items: SearchItem[] = [];
    for (const page of allPages) {
      for (const node of page.nodes) {
        const label = typeof node.data.label === 'string' ? node.data.label : '';
        items.push({ id: node.id, name: label || '(unnamed)', kind: (node.type as string) ?? 'process', pageId: page.id, pageName: page.name });
      }
      for (const edge of page.edges) {
        const label = typeof edge.label === 'string' && edge.label ? edge.label : 'data flow';
        items.push({ id: edge.id, name: label, kind: 'flow', pageId: page.id, pageName: page.name });
      }
    }
    return items;
  }, [allPages]);

  // The elements and flows a manual threat can be scoped to (reuses the canvas search index).
  const scopeOptions = useMemo<ThreatScopeOption[]>(
    () => searchItems.map((item) => ({ id: item.id, label: `${item.name} · ${item.kind}` })),
    [searchItems],
  );

  // Author a new manual threat: mint a stable manual id, record it on the overlay (so it persists and
  // round-trips), and show it in the panel immediately.
  const addThreat = useCallback(
    (draft: NewThreatDraft) => {
      const id = `manual:${crypto.randomUUID()}`;
      const elementIds = draft.scopeId ? [draft.scopeId] : [];
      const scope = scopeOptions.find((option) => option.id === draft.scopeId);
      const interaction = draft.scopeId ? scope?.label ?? draft.scopeId : 'Model-wide';
      const entry: ThreatTriage = {
        id,
        state: 'Open',
        manual: true,
        category: draft.category,
        title: draft.title,
        elementIds,
      };
      if (draft.priority) {
        entry.priority = draft.priority;
      }
      if (draft.description) {
        entry.description = draft.description;
      }
      if (draft.mitigation) {
        entry.mitigation = draft.mitigation;
      }
      setThreatTriage((prev) => [...prev, entry]);
      setThreats((prev) => [
        ...prev,
        {
          id,
          ruleId: '',
          category: draft.category,
          title: draft.title,
          mitigation: draft.mitigation,
          description: draft.description,
          severity: 'warning',
          priority: draft.priority ?? 'Medium',
          references: [],
          elementIds,
          interaction,
          state: 'Open',
          manual: true,
        },
      ]);
    },
    [scopeOptions],
  );

  // Jump to a searched element: switch to its page if needed, select it, and frame it in view.
  const jumpToSearchItem = useCallback(
    (item: SearchItem) => {
      const focus = () => {
        if (item.kind === 'flow') {
          setNodes((nds) => nds.map((n) => (n.selected ? { ...n, selected: false } : n)));
          setEdges((eds) => eds.map((e) => (e.selected !== (e.id === item.id) ? { ...e, selected: e.id === item.id } : e)));
          setSelection({ node: null, edge: item.id });
          const edge = allPages.find((p) => p.id === item.pageId)?.edges.find((e) => e.id === item.id);
          const ends = [edge?.source, edge?.target].filter((id): id is string => Boolean(id)).map((id) => ({ id }));
          if (ends.length > 0) {
            fitView({ nodes: ends, padding: 0.5, duration: 400, maxZoom: 1.4 });
          }
        } else {
          setEdges((eds) => eds.map((e) => (e.selected ? { ...e, selected: false } : e)));
          setNodes((nds) => nds.map((n) => (n.selected !== (n.id === item.id) ? { ...n, selected: n.id === item.id } : n)));
          setSelection({ node: item.id, edge: null });
          fitView({ nodes: [{ id: item.id }], padding: 0.6, duration: 400, maxZoom: 1.4 });
        }
      };
      if (item.pageId !== activePageId) {
        switchPage(item.pageId);
        window.setTimeout(focus, 80);
      } else {
        focus();
      }
    },
    [allPages, activePageId, switchPage, setNodes, setEdges, fitView],
  );

  const exportAs = useCallback(
    async (formatId: string) => {
      const format = formats.find((f) => f.id === formatId);
      try {
        const blob = await engine.convert(currentModel, formatId);
        downloadBlob(blob, `model${format?.extensions[0] ?? ''}`);
      } catch (err) {
        toast(err instanceof Error ? err.message : String(err), 'error');
      }
    },
    [engine, currentModel, formats],
  );

  // Download a self-contained HTML threat report from the engine. The offline client rejects with a
  // hint to start the engine, which surfaces as a toast — the same seam Export/Analyze already use.
  const downloadReport = useCallback(async () => {
    try {
      const blob = await engine.report(currentModel, 'html');
      downloadBlob(blob, 'threat-model-report.html');
    } catch (err) {
      toast(err instanceof Error ? err.message : String(err), 'error');
    }
  }, [engine, currentModel]);

  const loadModel = useCallback(
    (model: TmForgeModel) => {
      // Auto-fit each page as it loads: grow shapes so a name never overruns its boundary, route
      // flows through the ports that face their endpoints, and pull apart overlapping flow labels, so
      // an imported (for example, CLI-authored) model is readable without manual clean-up. `grow`
      // never shrinks a hand-tuned size, and labels already dragged aside are left as they are.
      const nextPages = pagesFromModel(model).map((p) => {
        const tidied = tidyGraph(p.nodes, p.edges, 'grow');
        return { ...p, nodes: tidied.nodes, edges: tidied.edges };
      });
      const nextPacks = model.analysis?.disabledPacks ?? [];
      const nextRuleIds = model.analysis?.disabledRuleIds ?? [];
      const first = nextPages[0];
      setPages(nextPages);
      setActivePageId(first.id);
      setNodes(first.nodes);
      setEdges(first.edges);
      setDisabledRulePacks(nextPacks);
      setDisabledRuleIds(nextRuleIds);
      setFindings([]);
      setThreats([]);
      setThreatTriage(model.threats ?? []);
      flaggedIdsRef.current = new Set();
      analysisActiveRef.current = false;
      setSelection({ node: null, edge: null });
      reset();
      // A freshly loaded model is the new saved baseline, so it does not read as dirty.
      setSavedJson(JSON.stringify(modelFromPages(nextPages, buildAnalysis(nextPacks, nextRuleIds))));
      window.setTimeout(() => fitView({ padding: 0.25, maxZoom: 1.15, duration: 300 }), 0);
    },
    [setNodes, setEdges, fitView, reset],
  );

  const readModelFromBytes = useCallback(
    async (bytes: Uint8Array, formatId: string): Promise<TmForgeModel> => {
      // The native tmforge-json is parsed client-side so the per-model analysis selection is
      // preserved; other formats round-trip through the engine (which projects onto tmforge-json).
      if (formatId === 'tmforge-json') {
        return engine.read(new TextDecoder().decode(bytes));
      }
      return engine.readFile(bytes, formatId);
    },
    [engine],
  );

  const onImportFile = useCallback(
    async (file: File) => {
      try {
        const bytes = new Uint8Array(await file.arrayBuffer());
        const detected = await engine.detect(bytes).catch(() => null);
        const formatId = detected?.id ?? 'tmforge-json';
        loadModel(await readModelFromBytes(bytes, formatId));
        // A hidden <input> gives no writable handle, so Save falls back to Save As / download.
        fileHandleRef.current = null;
        fileFormatRef.current = detected?.canWrite ? formatId : 'tmforge-json';
        setFileName(file.name);
      } catch (err) {
        toast(err instanceof Error ? err.message : 'Could not open that file.', 'error');
      }
    },
    [engine, loadModel, readModelFromBytes],
  );

  // Prefer the File System Access API so Open retains a writable handle (Save can then overwrite the
  // same file); browsers without it (Firefox/Safari) fall back to the hidden <input>.
  const openFile = useCallback(async () => {
    const picker = window as unknown as FilePickerWindow;
    if (!picker.showOpenFilePicker) {
      fileRef.current?.click();
      return;
    }
    try {
      const [handle] = await picker.showOpenFilePicker();
      const file = await handle.getFile();
      const bytes = new Uint8Array(await file.arrayBuffer());
      const detected = await engine.detect(bytes).catch(() => null);
      const formatId = detected?.id ?? 'tmforge-json';
      loadModel(await readModelFromBytes(bytes, formatId));
      fileHandleRef.current = handle;
      fileFormatRef.current = detected?.canWrite ? formatId : 'tmforge-json';
      setFileName(handle.name);
    } catch (err) {
      if (!isAbortError(err)) {
        toast(err instanceof Error ? err.message : 'Could not open that file.', 'error');
      }
    }
  }, [engine, loadModel, readModelFromBytes]);

  const clearAll = useCallback(() => {
    const id = crypto.randomUUID();
    setPages([{ id, name: 'Page 1', nodes: [], edges: [] }]);
    setActivePageId(id);
    setNodes([]);
    setEdges([]);
    setFindings([]);
    setThreats([]);
    setThreatTriage([]);
    flaggedIdsRef.current = new Set();
    analysisActiveRef.current = false;
    setSelection({ node: null, edge: null });
    reset();
  }, [setNodes, setEdges, reset]);

  // Auto-layout the active page: fit every shape to its label (so a name can't overrun its boundary),
  // route each flow through the ports that face its endpoints, and separate flow labels that overlap.
  // One undo snapshot covers the whole tidy, then the view is re-framed. This is the on-demand form
  // of the grow-only pass that runs when a model is loaded.
  const tidyActivePage = useCallback(() => {
    if (nodes.length === 0) {
      return;
    }
    takeSnapshot();
    const tidied = tidyGraph(nodes, edges, 'exact');
    setNodes(tidied.nodes);
    setEdges(tidied.edges);
    window.setTimeout(() => fitView({ padding: 0.25, maxZoom: 1.15, duration: 300 }), 0);
  }, [nodes, edges, setNodes, setEdges, takeSnapshot, fitView]);

  const actions = useMemo<DfdActions>(
    () => ({ beginEdit: takeSnapshot, renameNode, renameEdge, setEdgeLabelOffset }),
    [takeSnapshot, renameNode, renameEdge, setEdgeLabelOffset],
  );

  const selectedNode = nodes.find((n) => n.id === selection.node) ?? null;
  const selectedEdge = edges.find((e) => e.id === selection.edge) ?? null;

  return (
    <DfdActionsContext.Provider value={actions}>
    <div className="app">
      <Toolbar
        engineLabel={engine.label}
        engineOnline={engineOnline}
        demo={import.meta.env.VITE_DEMO === 'true'}
        exportFormats={formats.filter((f) => f.canWrite).map((f) => ({ id: f.id, displayName: f.displayName }))}
        onExport={exportAs}
        onImport={openFile}
        onSave={saveModel}
        onMerge={() => setShowMerge(true)}
        dirty={dirty}
        fileName={fileName}
        onAnalyze={runAnalyze}
        onReport={downloadReport}
        onClear={clearAll}
        onFit={() => fitView({ padding: 0.25, maxZoom: 1.15, duration: 300 })}
        onTidy={tidyActivePage}
        onUndo={undo}
        onRedo={redo}
        canUndo={canUndo}
        canRedo={canRedo}
        theme={theme}
        onToggleTheme={toggleTheme}
      />
      <div className="body">
        <Palette
          stencils={stencils}
          packs={packs}
          recentIds={recentStencilIds}
          favoriteIds={favoriteIds}
          disabledPacks={disabledPacks}
          onTogglePack={togglePack}
          onToggleFavorite={toggleFavorite}
        />
        <div className="canvas">
          <div className="canvas-flow" onDrop={onDrop} onDragOver={onDragOver}>
          <ReactFlow<DfdNode, DfdEdge>
            nodes={nodes}
            edges={edges}
            onNodesChange={onNodesChange}
            onEdgesChange={onEdgesChange}
            onConnect={onConnect}
            onReconnect={onReconnect}
            onNodeDragStart={takeSnapshot}
            onNodesDelete={takeSnapshot}
            onEdgesDelete={takeSnapshot}
            onSelectionChange={onSelectionChange}
            onPaneClick={findings.length || threats.length ? clearFlags : undefined}
            nodeTypes={nodeTypes}
            edgeTypes={edgeTypes}
            defaultEdgeOptions={defaultEdgeOptions}
            connectionMode={ConnectionMode.Loose}
            elevateNodesOnSelect={false}
            snapToGrid
            snapGrid={[GRID_SIZE, GRID_SIZE]}
            minZoom={0.2}
            maxZoom={2.5}
            colorMode={theme}
            fitView
            fitViewOptions={{ padding: 0.25, maxZoom: 1.15 }}
          >
            <Background variant={BackgroundVariant.Dots} gap={GRID_SIZE} size={1} color={theme === 'dark' ? '#26344c' : '#cbd5e1'} />
            <MiniMap
              pannable
              zoomable
              nodeStrokeWidth={2}
              nodeColor={(n) =>
                n.type === 'boundary' ? 'rgba(100, 116, 139, 0.15)' : KIND_COLOR[(n.type as DfdKind) ?? 'process'] ?? '#94a3b8'
              }
              nodeStrokeColor={(n) => KIND_COLOR[(n.type as DfdKind) ?? 'process'] ?? '#94a3b8'}
              maskColor={theme === 'dark' ? 'rgba(0, 0, 0, 0.4)' : 'rgba(15, 23, 42, 0.06)'}
              bgColor={theme === 'dark' ? '#0f1728' : '#f8fafc'}
            />
            <Controls />
            <Panel position="top-left">
              <CanvasSearch items={searchItems} onJump={jumpToSearchItem} />
            </Panel>
            <Panel position="top-right">
                <div className="val-panel">
                  <button
                    type="button"
                    className="val-panel-toggle"
                    onClick={() => setShowRules((v) => !v)}
                    aria-expanded={showRules}
                  >
                    <span className={`val-caret${showRules ? ' open' : ''}`} aria-hidden>
                      ▸
                    </span>
                    Analysis Rules
                    {disabledRulePacks.length + disabledRuleIds.length > 0 ? (
                      <span className="val-off-count">{disabledRulePacks.length + disabledRuleIds.length} off</span>
                    ) : null}
                  </button>
                  {showRules && (
                    <AnalysisSettings
                      rules={rules}
                      packs={rulePacks}
                      disabledPacks={disabledRulePacks}
                      disabledRuleIds={disabledRuleIds}
                      onTogglePack={toggleRulePack}
                      onToggleRule={toggleRule}
                    />
                  )}
                  {(threats.length > 0 || findings.length > 0) && (
                    <ThreatsPanel
                      threats={threats}
                      findings={findings}
                      onSelect={jumpToElements}
                      offPageLabel={offPageLabel}
                      onEditThreat={editThreat}
                      onAddThreat={addThreat}
                      onDeleteThreat={deleteThreat}
                      scopeOptions={scopeOptions}
                    />
                  )}
                </div>
            </Panel>
          </ReactFlow>
          <input
            ref={fileRef}
            type="file"
            accept={
              formats
                .filter((f) => f.canRead)
                .flatMap((f) => f.extensions)
                .join(',') || '.json,.tm7,.drawio,.vsdx'
            }
            hidden
            onChange={(e) => {
              const file = e.target.files?.[0];
              if (file) {
                void onImportFile(file);
              }
              e.target.value = '';
            }}
          />
          {nodes.length === 0 && (
            <div className="canvas-empty">
              <div className="canvas-empty-card">
                <div className="canvas-empty-icon" aria-hidden>
                  <svg
                    width="34"
                    height="34"
                    viewBox="0 0 24 24"
                    fill="none"
                    stroke="currentColor"
                    strokeWidth={1.6}
                    strokeLinecap="round"
                    strokeLinejoin="round"
                  >
                    <rect x="3" y="3" width="7" height="7" rx="1.5" />
                    <rect x="14" y="3" width="7" height="7" rx="1.5" />
                    <rect x="14" y="14" width="7" height="7" rx="1.5" />
                    <path d="M6.5 10v4h8" />
                  </svg>
                </div>
                <h2>Start your threat model</h2>
                <p>Drag a stencil from the left onto the canvas to begin.</p>
                <div className="canvas-empty-actions">
                  <button className="btn btn-primary" onClick={openFile}>
                    Open a file
                  </button>
                </div>
              </div>
            </div>
          )}
          </div>
          <PageTabs
            pages={pages}
            activePageId={activePageId}
            findingPageIds={findingPageIds}
            onSwitch={switchPage}
            onAdd={addPage}
            onRename={renamePage}
            onDelete={deletePage}
            onReorder={reorderPage}
          />
        </div>
        <Inspector
          node={selectedNode}
          edge={selectedEdge}
          stencils={stencils}
          propertySchema={propertySchema}
          onBeginNameEdit={takeSnapshot}
          onRenameNode={renameNode}
          onRenameEdge={renameEdge}
          onSetEdgeProperty={setEdgeProperty}
          onSetNodeProperty={setNodeProperty}
          onRemoveNodeProperty={removeNodeProperty}
          onDelete={deleteSelected}
        />
      </div>
    </div>
    {showMerge ? (
      <MergeResolveModal
        engine={engine}
        onClose={() => setShowMerge(false)}
        onResolved={(model) => {
          loadModel(model);
          // A merged model has no writable file handle yet, so Save falls back to Save As / download.
          fileHandleRef.current = null;
          fileFormatRef.current = 'tmforge-json';
          setFileName('merged.tm7');
          setShowMerge(false);
          toast('Loaded the merged model into the editor.', 'success');
        }}
      />
    ) : null}
    <Toaster />
    </DfdActionsContext.Provider>
  );
}
