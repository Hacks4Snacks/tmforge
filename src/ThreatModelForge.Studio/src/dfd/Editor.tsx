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
  useNodesState,
  useEdgesState,
  useReactFlow,
  ConnectionMode,
  MarkerType,
  type Connection,
  type NodeTypes,
  type EdgeTypes,
  type DefaultEdgeOptions,
  type XYPosition,
} from '@xyflow/react';
import '@xyflow/react/dist/style.css';
import type { DragEvent as ReactDragEvent } from 'react';
import { ShapeNode } from './nodes/ShapeNode';
import { TrustBoundaryNode } from './nodes/TrustBoundaryNode';
import { Palette } from './Palette';
import { Toolbar } from './Toolbar';
import { Inspector } from './Inspector';
import { ValidationSettings } from './ValidationSettings';
import { FALLBACK_PACKS, FALLBACK_STENCILS } from './stencils';
import { createHttpEngine, loadWasmEngine, offlineEngine, probeEngine, type Finding, type FormatInfo, type IEngineClient, type PackInfo, type PropertyDescriptorInfo, type RuleInfo, type RulePackInfo, type StencilInfo } from './engineClient';
import { DEFAULT_NODE_SIZE, fromModel, toModel } from './mapping';
import { useUndoRedo } from './useUndoRedo';
import { FlowEdge } from './edges/FlowEdge';
import { DfdActionsContext, type DfdActions } from './editorContext';
import { Toaster, toast } from './toast';
import type { DfdEdge, DfdKind, DfdNode, TmForgeModel, TmForgeValidation } from './types';

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

const EMPTY_MODEL: TmForgeModel = {
  schema: 'tmforge-json',
  version: '0.1',
  elements: [],
  flows: [],
};

const STORAGE_KEY = 'tmforge.studio.model.v1';
const THEME_KEY = 'tmforge.studio.theme';
const RECENTS_KEY = 'tmforge.studio.recentStencils.v1';
/** How many recently used stencils to remember. */
const RECENTS_MAX = 6;
const PACKS_DISABLED_KEY = 'tmforge.studio.disabledPacks.v1';
const FAVORITES_KEY = 'tmforge.studio.favoriteStencils.v1';

type Theme = 'light' | 'dark';

/** The initial theme: a saved choice if present, else the OS preference. */
function initialTheme(): Theme {
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

/** Reads a previously saved model from browser storage, or null when none/invalid. */
function loadStoredModel(): TmForgeModel | null {
  try {
    const raw = window.localStorage.getItem(STORAGE_KEY);
    if (!raw) {
      return null;
    }
    const parsed = JSON.parse(raw) as TmForgeModel;
    return parsed?.schema === 'tmforge-json' ? parsed : null;
  } catch {
    return null;
  }
}

/** Reads the recently used stencil ids from browser storage (most recent first). */
function loadRecentStencilIds(): string[] {
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
function loadStringList(key: string): string[] {
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
function persistStringList(key: string, value: string[]): void {
  try {
    window.localStorage.setItem(key, JSON.stringify(value));
  } catch {
    /* storage unavailable */
  }
}

// Restore the last saved model on load, else start from an empty canvas.
const INITIAL_MODEL = loadStoredModel() ?? EMPTY_MODEL;
const INITIAL = fromModel(INITIAL_MODEL);
const INITIAL_SAVED_JSON = JSON.stringify(
  modelOf(
    INITIAL.nodes,
    INITIAL.edges,
    INITIAL_MODEL.validation?.disabledPacks ?? [],
    INITIAL_MODEL.validation?.disabledRuleIds ?? [],
  ),
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
function isAbortError(err: unknown): boolean {
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

/** Builds the validation block from the current selection, or undefined when nothing is disabled. */
function buildValidation(disabledPacks: string[], disabledRuleIds: string[]): TmForgeValidation | undefined {
  const validation: TmForgeValidation = {};
  if (disabledPacks.length > 0) {
    validation.disabledPacks = disabledPacks;
  }
  if (disabledRuleIds.length > 0) {
    validation.disabledRuleIds = disabledRuleIds;
  }
  return validation.disabledPacks || validation.disabledRuleIds ? validation : undefined;
}

/** The canonical model for the current graph plus the per-model validation selection. */
function modelOf(
  nodes: DfdNode[],
  edges: DfdEdge[],
  disabledPacks: string[],
  disabledRuleIds: string[],
): TmForgeModel {
  const model = toModel(nodes, edges);
  const validation = buildValidation(disabledPacks, disabledRuleIds);
  return validation ? { ...model, validation } : model;
}

export function Editor() {
  const [nodes, setNodes, onNodesChange] = useNodesState<DfdNode>(INITIAL.nodes);
  const [edges, setEdges, onEdgesChange] = useEdgesState<DfdEdge>(INITIAL.edges);
  const [findings, setFindings] = useState<Finding[]>([]);
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
  const [disabledRulePacks, setDisabledRulePacks] = useState<string[]>(
    () => INITIAL_MODEL.validation?.disabledPacks ?? [],
  );
  const [disabledRuleIds, setDisabledRuleIds] = useState<string[]>(
    () => INITIAL_MODEL.validation?.disabledRuleIds ?? [],
  );
  const [showRules, setShowRules] = useState(false);
  const validationActiveRef = useRef(false);
  const fileRef = useRef<HTMLInputElement>(null);
  const fileHandleRef = useRef<WritableFileHandle | null>(null);
  const fileFormatRef = useRef<string>('tmforge-json');
  const [fileName, setFileName] = useState<string | null>(null);
  const { screenToFlowPosition, fitView } = useReactFlow();
  const { takeSnapshot, undo, redo, canUndo, canRedo } = useUndoRedo(nodes, edges, setNodes, setEdges);

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

  // Load the analysis rule catalog + rule packs (for the validation settings panel) from the engine.
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

  // Persistence. `currentJson` serializes the model the way Save writes it (toModel ignores
  // selection, so selecting a node never marks the model dirty); `dirty` compares it to the snapshot
  // from the last explicit Save. A debounced localStorage write runs on every change as a silent
  // crash-recovery net, so a reload never loses work regardless of saving to a file.
  const currentModel = useMemo(
    () => modelOf(nodes, edges, disabledRulePacks, disabledRuleIds),
    [nodes, edges, disabledRulePacks, disabledRuleIds],
  );
  const currentJson = useMemo(() => JSON.stringify(currentModel), [currentModel]);
  const [savedJson, setSavedJson] = useState(INITIAL_SAVED_JSON);
  const dirty = currentJson !== savedJson;

  useEffect(() => {
    const id = window.setTimeout(() => {
      try {
        window.localStorage.setItem(STORAGE_KEY, currentJson);
      } catch {
        /* storage unavailable (private mode / quota) — ignore */
      }
    }, 600);
    return () => window.clearTimeout(id);
  }, [currentJson]);

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
  // the file and sent on validate), so it is model state, not a persisted UI preference.
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
      setEdges((eds) => eds.map((e) => (e.id === id ? { ...e, data: { ...e.data, labelOffset: offset } } : e))),
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
    validationActiveRef.current = false;
  }, [setNodes, setEdges]);

  const runValidate = useCallback(async () => {
    let result: Finding[];
    try {
      result = await engine.validate(currentModel);
    } catch (err) {
      toast(err instanceof Error ? err.message : 'Validation failed.', 'error');
      return;
    }
    setFindings(result);
    validationActiveRef.current = true;
    const flagged = new Set(result.flatMap((f) => f.elementIds));
    setNodes((nds) => nds.map((n) => ({ ...n, className: flagged.has(n.id) ? 'flagged' : undefined })));
    setEdges((eds) => eds.map((e) => ({ ...e, className: flagged.has(e.id) ? 'flagged' : undefined })));
  }, [engine, currentModel, setNodes, setEdges]);

  // When a validation is already on screen, changing the rule selection re-runs it so the findings
  // reflect the new choice immediately. A ref holds the latest runValidate to avoid a dependency loop.
  const runValidateRef = useRef(runValidate);
  runValidateRef.current = runValidate;
  useEffect(() => {
    if (validationActiveRef.current) {
      void runValidateRef.current();
    }
  }, [disabledRulePacks, disabledRuleIds]);

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

  const loadModel = useCallback(
    (model: TmForgeModel) => {
      takeSnapshot();
      const next = fromModel(model);
      const nextPacks = model.validation?.disabledPacks ?? [];
      const nextRuleIds = model.validation?.disabledRuleIds ?? [];
      setNodes(next.nodes);
      setEdges(next.edges);
      setDisabledRulePacks(nextPacks);
      setDisabledRuleIds(nextRuleIds);
      setFindings([]);
      validationActiveRef.current = false;
      // A freshly loaded model is the new saved baseline, so it does not read as dirty.
      setSavedJson(JSON.stringify(modelOf(next.nodes, next.edges, nextPacks, nextRuleIds)));
      window.setTimeout(() => fitView({ padding: 0.25, maxZoom: 1.15, duration: 300 }), 0);
    },
    [setNodes, setEdges, fitView, takeSnapshot],
  );

  const readModelFromBytes = useCallback(
    async (bytes: Uint8Array, formatId: string): Promise<TmForgeModel> => {
      // The native tmforge-json is parsed client-side so the per-model validation selection is
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
    takeSnapshot();
    setNodes([]);
    setEdges([]);
    setFindings([]);
  }, [setNodes, setEdges, takeSnapshot]);

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
        dirty={dirty}
        fileName={fileName}
        onValidate={runValidate}
        onClear={clearAll}
        onFit={() => fitView({ padding: 0.25, maxZoom: 1.15, duration: 300 })}
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
        <div className="canvas" onDrop={onDrop} onDragOver={onDragOver}>
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
            onPaneClick={findings.length ? clearFlags : undefined}
            nodeTypes={nodeTypes}
            edgeTypes={edgeTypes}
            defaultEdgeOptions={defaultEdgeOptions}
            connectionMode={ConnectionMode.Loose}
            elevateNodesOnSelect={false}
            minZoom={0.2}
            maxZoom={2.5}
            colorMode={theme}
            fitView
            fitViewOptions={{ padding: 0.25, maxZoom: 1.15 }}
          >
            <Background variant={BackgroundVariant.Dots} gap={16} size={1} color={theme === 'dark' ? '#26344c' : '#cbd5e1'} />
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
                    Validation
                    {disabledRulePacks.length + disabledRuleIds.length > 0 ? (
                      <span className="val-off-count">{disabledRulePacks.length + disabledRuleIds.length} off</span>
                    ) : null}
                  </button>
                  {showRules && (
                    <ValidationSettings
                      rules={rules}
                      packs={rulePacks}
                      disabledPacks={disabledRulePacks}
                      disabledRuleIds={disabledRuleIds}
                      onTogglePack={toggleRulePack}
                      onToggleRule={toggleRule}
                    />
                  )}
                  {findings.length > 0 && (
                    <div className="findings">
                      <h3>
                        {findings.length} finding{findings.length === 1 ? '' : 's'}
                      </h3>
                      {findings.map((f) => (
                        <div key={f.id} className="finding">
                          <span className={`sev sev-${f.severity}`}>{f.severity}</span>
                          <span>
                            {f.ruleId ? <code className="rule-id">{f.ruleId}</code> : null} {f.message}
                          </span>
                        </div>
                      ))}
                    </div>
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
    <Toaster />
    </DfdActionsContext.Provider>
  );
}
