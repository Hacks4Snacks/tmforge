import createClient, { type Client } from 'openapi-fetch';
import { FALLBACK_PACKS, FALLBACK_STENCILS } from './stencils';
import type { DfdKind, TmForgeModel } from './types';
import type { components, paths } from './engine/schema';

export type Severity = 'info' | 'warning' | 'error';

export interface Finding {
  id: string;
  severity: Severity;
  /** The originating engine rule id (e.g. TM-xxxx), when the finding came from the real engine. */
  ruleId?: string;
  message: string;
  /** ids of elements/flows this finding refers to, so the UI can highlight them. */
  elementIds: string[];
}

/** A single point a three-way merge could not reconcile automatically (the merge kept `ours`). */
export interface MergeConflict {
  /** The stable id of the element the conflict concerns. */
  elementId: string;
  /** The element kind ('process' | 'store' | 'external' | 'boundary' | 'flow'). */
  elementKind: string;
  /** The element's display name. */
  name: string;
  /** The diagram (page) the element belongs to. */
  diagramName: string;
  /** 'Property' (same attribute, different values) | 'DeleteModify' | 'AddAdd' | 'DanglingReference'. */
  kind: string;
  /** The attribute key in conflict (e.g. 'name', 'Protocol', 'source', 'target'); empty for a structural conflict. */
  property: string;
  /** The ancestor value, when applicable. */
  base?: string;
  /** The `ours` value the merge kept. */
  ours?: string;
  /** The `theirs` value the merge dropped. */
  theirs?: string;
}

/** The result of a three-way merge: the merged model plus any conflicts (all resolved to `ours`). */
export interface MergeResult {
  merged: TmForgeModel;
  conflicts: MergeConflict[];
}

/** A file format the engine can read and/or write. */
export interface FormatInfo {
  id: string;
  displayName: string;
  extensions: string[];
  canRead: boolean;
  canWrite: boolean;
}

/** An authoring stencil: a categorized specialization of one of the four DFD primitives. */
export interface StencilInfo {
  id: string;
  /** The underlying DFD primitive this stencil maps to (drives analysis + rendering). */
  base: DfdKind;
  label: string;
  category: string;
  /** The stencil pack this stencil ships in (for example, 'azure'). */
  pack: string;
  blurb: string;
  /** Free-form search tags and aliases. */
  tags: string[];
  /** Preset custom properties applied when the stencil is placed. */
  defaults: Record<string, string>;
}

/** A stencil pack: a named, togglable group of related stencils shown in the palette. */
export interface PackInfo {
  id: string;
  name: string;
  /** How many stencils the pack contributes. */
  count: number;
}

/** An analysis rule offered by the engine (surfaced in the validation settings UI). */
export interface RuleInfo {
  id: string;
  /** The rule pack this rule belongs to (for example, 'security-properties'). */
  pack: string;
  severity: Severity;
  /** What the rule evaluates and why (the rule's full description). */
  description: string;
  /** How to clear a finding from this rule (shown in the in-app help). */
  helpText: string;
  helpUri?: string;
}

/** A rule pack: a named, selectable group of related analysis rules. */
export interface RulePackInfo {
  id: string;
  name: string;
  /** How many rules the pack contributes. */
  count: number;
}

/** A typed element-property definition: drives typed Inspector controls and canonical values. */
export interface PropertyDescriptorInfo {
  /** The DFD primitive this property applies to ('process' | 'datastore' | 'external' | 'flow'). */
  appliesTo: string;
  /** The property key (custom-attribute name), for example 'AuthenticationScheme'. */
  name: string;
  /** The value kind that drives the control: 'enum' | 'bool' | 'string'. */
  kind: string;
  /** Allowed values for an enum/bool property; empty for free-form string. */
  values: string[];
  /** The default value applied when the property is first added. */
  default: string;
}

/**
 * The seam. The UI depends only on this interface — never on how the engine is reached.
 *
 * Implementations that ship here:
 *   - `HttpEngineClient` - the real .NET engine over the `/v1` API.
 *   - `WasmEngineClient` - the same engine compiled to WebAssembly, in-browser, no backend.
 *   - `OfflineEngineClient` - honest fallback when neither is reachable: client-side authoring only.
 *
 * `read`/`write` just (de)serialize the canonical `tmforge-json` client-side — no engine needed —
 * so all clients share them. Everything else (`validate`, `getFormats`, `detect`, `readFile`,
 * `convert`, `report`, `exportTm7`) is a coarse-grained engine call.
 */
export interface IEngineClient {
  readonly label: string;
  write(model: TmForgeModel): Promise<string>;
  read(text: string): Promise<TmForgeModel>;
  validate(model: TmForgeModel): Promise<Finding[]>;
  exportTm7(model: TmForgeModel): Promise<Blob>;
  /** Lists the engine's registered file formats and their capabilities. */
  getFormats(): Promise<FormatInfo[]>;
  /** Lists the authoring stencil catalog offered to the palette. */
  getStencils(): Promise<StencilInfo[]>;
  /** Lists the stencil packs offered to the palette (for show/hide toggles). */
  getStencilPacks(): Promise<PackInfo[]>;
  /** Lists the analysis rules offered by the engine (for the validation settings UI). */
  getRules(): Promise<RuleInfo[]>;
  /** Lists the rule packs offered by the engine (for per-model validation toggles). */
  getRulePacks(): Promise<RulePackInfo[]>;
  /** Lists the typed element-property schema (drives typed Inspector controls + canonical values). */
  getPropertySchema(): Promise<PropertyDescriptorInfo[]>;
  /** Detects the format of raw document bytes, or null when none matches. */
  detect(bytes: Uint8Array): Promise<FormatInfo | null>;
  /** Reads a document in any registered format into the canonical tmforge-json model. */
  readFile(bytes: Uint8Array, formatId?: string): Promise<TmForgeModel>;
  /** Serializes the model to another registered format (for example tm7, drawio, vsdx). */
  convert(model: TmForgeModel, toFormatId: string): Promise<Blob>;
  /** Renders an HTML or SVG report for the model. */
  report(model: TmForgeModel, format: 'html' | 'svg'): Promise<Blob>;
  /**
   * Merges two edited models, matched by element id. With a `base` (common ancestor) it is a
   * three-way merge so non-overlapping edits combine automatically; pass `null` when the ancestor
   * is unavailable for a two-way merge, where any overlapping difference is reported as a conflict.
   */
  merge(base: TmForgeModel | null, ours: TmForgeModel, theirs: TmForgeModel): Promise<MergeResult>;
}

/**
 * Origin of the engine API. In production the SPA is served by the API itself, so this is the same
 * origin (empty string -> relative `/v1/...`). In `vite dev` the SPA runs on :5199 while the API
 * runs on :5205, so target that explicitly. The generated OpenAPI paths already carry the `/v1`
 * prefix.
 */
export const ENGINE_BASE_URL = import.meta.env.DEV ? 'http://localhost:5205' : '';

async function writeJson(model: TmForgeModel): Promise<string> {
  return JSON.stringify(model, null, 2);
}

async function readJson(text: string): Promise<TmForgeModel> {
  const parsed = JSON.parse(text) as TmForgeModel;
  if (parsed?.schema !== 'tmforge-json') {
    throw new Error('Not a tmforge-json document.');
  }
  return parsed;
}

/** Encodes bytes as base64 for the engine's read/detect payloads. */
function toBase64(bytes: Uint8Array): string {
  let binary = '';
  for (let i = 0; i < bytes.length; i += 1) {
    binary += String.fromCharCode(bytes[i]);
  }
  return btoa(binary);
}

/** Decodes a base64 string (the WASM boundary's byte encoding) into a typed Blob for download. */
function blobFromBase64(base64: string, type: string): Blob {
  const binary = atob(base64);
  const bytes = new Uint8Array(binary.length);
  for (let i = 0; i < binary.length; i += 1) {
    bytes[i] = binary.charCodeAt(i);
  }
  return new Blob([bytes], { type });
}

/** The download content-type for a converted document (mirrors the /v1 convert endpoint). */
function mimeForFormat(formatId: string): string {
  switch (formatId) {
    case 'vsdx':
      return 'application/vnd.ms-visio.drawing';
    case 'tmforge-json':
      return 'application/json';
    default:
      return 'application/xml';
  }
}

/** Normalizes a generated FormatDto (all fields optional) onto the UI's FormatInfo. */
function toFormatInfo(dto: components['schemas']['FormatDto']): FormatInfo {
  return {
    id: dto.id ?? '',
    displayName: dto.displayName ?? '',
    extensions: dto.extensions ?? [],
    canRead: dto.canRead ?? false,
    canWrite: dto.canWrite ?? false,
  };
}

/** Normalizes a generated StencilDto (all fields optional) onto the UI's StencilInfo. */
function toStencilInfo(dto: components['schemas']['StencilDto']): StencilInfo {
  return {
    id: dto.id ?? '',
    base: (dto.base ?? 'process') as DfdKind,
    label: dto.label ?? '',
    category: dto.category ?? 'Other',
    pack: dto.pack ?? 'generic',
    blurb: dto.blurb ?? '',
    tags: dto.tags ?? [],
    defaults: dto.defaults ?? {},
  };
}

/** Normalizes a generated PackDto (all fields optional) onto the UI's PackInfo. */
function toPackInfo(dto: components['schemas']['PackDto']): PackInfo {
  return {
    id: dto.id ?? '',
    name: dto.name ?? '',
    count: Number(dto.count ?? 0),
  };
}

/** Normalizes a generated RuleDto (all fields optional) onto the UI's RuleInfo. */
function toRuleInfo(dto: components['schemas']['RuleDto']): RuleInfo {
  return {
    id: dto.id ?? '',
    pack: dto.pack ?? '',
    severity: (dto.severity ?? 'info') as Severity,
    description: dto.description ?? '',
    helpText: dto.helpText ?? '',
    helpUri: dto.helpUri ?? undefined,
  };
}

/** Normalizes a generated RulePackDto (all fields optional) onto the UI's RulePackInfo. */
function toRulePackInfo(dto: components['schemas']['RulePackDto']): RulePackInfo {
  return {
    id: dto.id ?? '',
    name: dto.name ?? '',
    count: Number(dto.count ?? 0),
  };
}

/** Normalizes a generated PropertyDescriptor (all fields optional) onto the UI's PropertyDescriptorInfo. */
function toPropertyDescriptor(dto: components['schemas']['PropertyDescriptor']): PropertyDescriptorInfo {
  return {
    appliesTo: dto.appliesTo ?? '',
    name: dto.name ?? '',
    kind: dto.kind ?? 'string',
    values: dto.values ?? [],
    default: dto.default ?? '',
  };
}

/** Normalizes a generated TmForgeModelDto (all fields optional/nullable) onto the UI model. */
function toModel(dto: components['schemas']['TmForgeModelDto']): TmForgeModel {
  return {
    schema: 'tmforge-json',
    version: '0.1',
    elements: (dto.elements ?? []).map((e) => ({
      id: e.id ?? '',
      kind: (e.kind ?? 'process') as DfdKind,
      name: e.name ?? '',
      x: Number(e.x ?? 0),
      y: Number(e.y ?? 0),
      width: e.width == null ? undefined : Number(e.width),
      height: e.height == null ? undefined : Number(e.height),
      properties: e.properties ?? {},
    })),
    flows: (dto.flows ?? []).map((f) => ({
      id: f.id ?? '',
      source: f.source ?? '',
      target: f.target ?? '',
      name: f.name ?? '',
      properties: f.properties ?? {},
    })),
    validation: dto.validation
      ? {
          disabledPacks: dto.validation.disabledPacks ?? undefined,
          disabledRuleIds: dto.validation.disabledRuleIds ?? undefined,
        }
      : undefined,
  };
}

/** Normalizes a generated MergeResultDto (all fields optional/nullable) onto the UI's MergeResult. */
function toMergeResult(dto: components['schemas']['MergeResultDto']): MergeResult {
  return {
    merged: toModel(dto.merged ?? ({} as components['schemas']['TmForgeModelDto'])),
    conflicts: (dto.conflicts ?? []).map((c) => ({
      elementId: c.elementId ?? '',
      elementKind: c.elementKind ?? '',
      name: c.name ?? '',
      diagramName: c.diagramName ?? '',
      kind: c.kind ?? '',
      property: c.property ?? '',
      base: c.base ?? undefined,
      ours: c.ours ?? undefined,
      theirs: c.theirs ?? undefined,
    })),
  };
}

class OfflineEngineClient implements IEngineClient {
  public readonly label = 'offline (engine unavailable)';

  public write(model: TmForgeModel): Promise<string> {
    return writeJson(model);
  }

  public read(text: string): Promise<TmForgeModel> {
    return readJson(text);
  }

  public validate(): Promise<Finding[]> {
    // No fake analysis: when neither the /v1 engine nor the in-browser WASM engine is reachable,
    // fail honestly rather than returning heuristics that disagree with the real rule set.
    return Promise.reject(
      new Error(
        'The analysis engine has not loaded. WebAssembly may be disabled or blocked (for example by a ' +
          'Content-Security-Policy), or is still downloading — reload the page, or use the hosted app.',
      ),
    );
  }

  public exportTm7(): Promise<Blob> {
    return Promise.reject(
      new Error('Real .tm7 export requires the .NET engine. Start the API (see the spike README), then reload.'),
    );
  }

  public async getFormats(): Promise<FormatInfo[]> {
    // Offline: only the canonical client-side format is available.
    return [
      {
        id: 'tmforge-json',
        displayName: 'Threat Model Forge JSON (.tmforge.json)',
        extensions: ['.tmforge.json'],
        canRead: true,
        canWrite: true,
      },
    ];
  }

  public async getStencils(): Promise<StencilInfo[]> {
    // Offline: fall back to the generic DFD primitives.
    return FALLBACK_STENCILS;
  }

  public async getStencilPacks(): Promise<PackInfo[]> {
    // Offline: only the generic pack is available.
    return FALLBACK_PACKS;
  }

  public async getRules(): Promise<RuleInfo[]> {
    // Offline: the stub uses canned heuristics, not the real engine rule catalog.
    return [];
  }

  public async getRulePacks(): Promise<RulePackInfo[]> {
    // Offline: no rule packs to configure without the engine.
    return [];
  }

  public async getPropertySchema(): Promise<PropertyDescriptorInfo[]> {
    // Offline: without the engine there is no typed schema; the Inspector falls back to free text.
    return [];
  }

  public async detect(bytes: Uint8Array): Promise<FormatInfo | null> {
    const text = new TextDecoder().decode(bytes);
    return text.includes('tmforge-json') ? (await this.getFormats())[0] : null;
  }

  public readFile(bytes: Uint8Array): Promise<TmForgeModel> {
    // Offline: only tmforge-json can be parsed client-side.
    return readJson(new TextDecoder().decode(bytes));
  }

  public convert(model: TmForgeModel, toFormatId: string): Promise<Blob> {
    if (toFormatId === 'tmforge-json') {
      return writeJson(model).then((text) => new Blob([text], { type: 'application/json' }));
    }

    return Promise.reject(
      new Error(`Converting to '${toFormatId}' requires the .NET engine. Start the API, then reload.`),
    );
  }

  public report(): Promise<Blob> {
    return Promise.reject(
      new Error('Reports require the .NET engine. Start the API (see the spike README), then reload.'),
    );
  }

  public merge(): Promise<MergeResult> {
    return Promise.reject(
      new Error('Three-way merge requires the .NET engine. Start the API (or use the hosted app), then reload.'),
    );
  }
}

class HttpEngineClient implements IEngineClient {
  public readonly label = 'engine (/v1)';

  private readonly client: Client<paths>;

  public constructor(baseUrl: string) {
    this.client = createClient<paths>({ baseUrl });
  }

  public write(model: TmForgeModel): Promise<string> {
    return writeJson(model);
  }

  public read(text: string): Promise<TmForgeModel> {
    return readJson(text);
  }

  public async validate(model: TmForgeModel): Promise<Finding[]> {
    const { data, response } = await this.client.POST('/v1/model/validate', { body: model });
    if (!response.ok) {
      throw new Error(`Engine validate failed (${response.status}).`);
    }
    // The generated FindingDto has every field optional/nullable; normalize onto the UI's Finding.
    return (data ?? []).map((f) => ({
      id: f.id ?? '',
      severity: (f.severity ?? 'info') as Severity,
      ruleId: f.ruleId ?? undefined,
      message: f.message ?? '',
      elementIds: f.elementIds ?? [],
    }));
  }

  public async exportTm7(model: TmForgeModel): Promise<Blob> {
    // parseAs 'stream' leaves the response body unconsumed so we can hand back a Blob for download.
    const { response } = await this.client.POST('/v1/model/export/tm7', {
      body: model,
      parseAs: 'stream',
    });
    if (!response.ok) {
      throw new Error(`Engine export failed (${response.status}).`);
    }
    return await response.blob();
  }

  public async getFormats(): Promise<FormatInfo[]> {
    const { data, response } = await this.client.GET('/v1/formats');
    if (!response.ok) {
      throw new Error(`Engine formats failed (${response.status}).`);
    }
    return (data ?? []).map(toFormatInfo);
  }

  public async getStencils(): Promise<StencilInfo[]> {
    const { data, response } = await this.client.GET('/v1/stencils');
    if (!response.ok) {
      throw new Error(`Engine stencils failed (${response.status}).`);
    }
    return (data ?? []).map(toStencilInfo);
  }

  public async getStencilPacks(): Promise<PackInfo[]> {
    const { data, response } = await this.client.GET('/v1/stencil-packs');
    if (!response.ok) {
      throw new Error(`Engine stencil packs failed (${response.status}).`);
    }
    return (data ?? []).map(toPackInfo);
  }

  public async getRules(): Promise<RuleInfo[]> {
    const { data, response } = await this.client.GET('/v1/rules');
    if (!response.ok) {
      throw new Error(`Engine rules failed (${response.status}).`);
    }
    return (data ?? []).map(toRuleInfo);
  }

  public async getRulePacks(): Promise<RulePackInfo[]> {
    const { data, response } = await this.client.GET('/v1/rule-packs');
    if (!response.ok) {
      throw new Error(`Engine rule packs failed (${response.status}).`);
    }
    return (data ?? []).map(toRulePackInfo);
  }

  public async getPropertySchema(): Promise<PropertyDescriptorInfo[]> {
    const { data, response } = await this.client.GET('/v1/property-schema');
    if (!response.ok) {
      throw new Error(`Engine property schema failed (${response.status}).`);
    }
    return (data ?? []).map(toPropertyDescriptor);
  }

  public async detect(bytes: Uint8Array): Promise<FormatInfo | null> {
    const { data, response } = await this.client.POST('/v1/detect', {
      body: { contentBase64: toBase64(bytes) },
    });
    if (response.status === 404) {
      return null;
    }
    if (!response.ok) {
      throw new Error(`Engine detect failed (${response.status}).`);
    }
    return data ? toFormatInfo(data) : null;
  }

  public async readFile(bytes: Uint8Array, formatId?: string): Promise<TmForgeModel> {
    const { data, response } = await this.client.POST('/v1/model/read', {
      body: { contentBase64: toBase64(bytes), formatId },
    });
    if (!response.ok || !data) {
      throw new Error(`Engine read failed (${response.status}).`);
    }
    return toModel(data);
  }

  public async convert(model: TmForgeModel, toFormatId: string): Promise<Blob> {
    const { response } = await this.client.POST('/v1/model/convert', {
      params: { query: { to: toFormatId } },
      body: model,
      parseAs: 'stream',
    });
    if (!response.ok) {
      throw new Error(`Engine convert failed (${response.status}).`);
    }
    return await response.blob();
  }

  public async report(model: TmForgeModel, format: 'html' | 'svg'): Promise<Blob> {
    const { response } = await this.client.POST('/v1/model/report', {
      params: { query: { format } },
      body: model,
      parseAs: 'stream',
    });
    if (!response.ok) {
      throw new Error(`Engine report failed (${response.status}).`);
    }
    return await response.blob();
  }

  public async merge(base: TmForgeModel | null, ours: TmForgeModel, theirs: TmForgeModel): Promise<MergeResult> {
    const { data, response } = await this.client.POST('/v1/model/merge', {
      body: { base: base ?? undefined, ours, theirs },
    });
    if (!response.ok || !data) {
      throw new Error(`Engine merge failed (${response.status}).`);
    }
    return toMergeResult(data);
  }
}

/** The `[JSExport]` methods on the WASM `ThreatModelForge.Wasm.Engine` type (all string in/out). */
interface WasmEngineExports {
  Ping(): string;
  Formats(): string;
  Stencils(): string;
  StencilPacks(): string;
  Rules(): string;
  RulePacks(): string;
  PropertySchema(): string;
  Validate(tmforgeJson: string): string;
  Detect(contentBase64: string): string;
  ReadFile(contentBase64: string, formatId: string): string;
  ExportTm7(tmforgeJson: string): string;
  ConvertModel(tmforgeJson: string, toFormatId: string): string;
  Report(tmforgeJson: string, format: string): string;
  Merge(baseJson: string, oursJson: string, theirsJson: string): string;
}

/**
 * Calls the real .NET engine compiled to WebAssembly, in-browser, with no network. It is the SAME
 * engine the `/v1` API runs (both go through the shared `ThreatModelForge.Engine` facade); only the
 * transport differs. tmforge-json crosses the boundary as a string, binary documents as base64.
 */
class WasmEngineClient implements IEngineClient {
  public readonly label = 'engine (wasm)';

  private readonly wasm: WasmEngineExports;

  public constructor(wasm: WasmEngineExports) {
    this.wasm = wasm;
  }

  public write(model: TmForgeModel): Promise<string> {
    return writeJson(model);
  }

  public read(text: string): Promise<TmForgeModel> {
    return readJson(text);
  }

  public async validate(model: TmForgeModel): Promise<Finding[]> {
    const findings = JSON.parse(this.wasm.Validate(JSON.stringify(model))) as Array<components['schemas']['FindingDto']>;
    return findings.map((f) => ({
      id: f.id ?? '',
      severity: (f.severity ?? 'info') as Severity,
      ruleId: f.ruleId ?? undefined,
      message: f.message ?? '',
      elementIds: f.elementIds ?? [],
    }));
  }

  public async exportTm7(model: TmForgeModel): Promise<Blob> {
    return blobFromBase64(this.wasm.ExportTm7(JSON.stringify(model)), 'application/xml');
  }

  public async getFormats(): Promise<FormatInfo[]> {
    return (JSON.parse(this.wasm.Formats()) as Array<components['schemas']['FormatDto']>).map(toFormatInfo);
  }

  public async getStencils(): Promise<StencilInfo[]> {
    return (JSON.parse(this.wasm.Stencils()) as Array<components['schemas']['StencilDto']>).map(toStencilInfo);
  }

  public async getStencilPacks(): Promise<PackInfo[]> {
    return (JSON.parse(this.wasm.StencilPacks()) as Array<components['schemas']['PackDto']>).map(toPackInfo);
  }

  public async getRules(): Promise<RuleInfo[]> {
    return (JSON.parse(this.wasm.Rules()) as Array<components['schemas']['RuleDto']>).map(toRuleInfo);
  }

  public async getRulePacks(): Promise<RulePackInfo[]> {
    return (JSON.parse(this.wasm.RulePacks()) as Array<components['schemas']['RulePackDto']>).map(toRulePackInfo);
  }

  public async getPropertySchema(): Promise<PropertyDescriptorInfo[]> {
    return (JSON.parse(this.wasm.PropertySchema()) as Array<components['schemas']['PropertyDescriptor']>).map(
      toPropertyDescriptor,
    );
  }

  public async detect(bytes: Uint8Array): Promise<FormatInfo | null> {
    const json = this.wasm.Detect(toBase64(bytes));
    return json ? toFormatInfo(JSON.parse(json) as components['schemas']['FormatDto']) : null;
  }

  public async readFile(bytes: Uint8Array, formatId?: string): Promise<TmForgeModel> {
    const dto = JSON.parse(this.wasm.ReadFile(toBase64(bytes), formatId ?? '')) as components['schemas']['TmForgeModelDto'];
    return toModel(dto);
  }

  public async convert(model: TmForgeModel, toFormatId: string): Promise<Blob> {
    return blobFromBase64(this.wasm.ConvertModel(JSON.stringify(model), toFormatId), mimeForFormat(toFormatId));
  }

  public async report(model: TmForgeModel, format: 'html' | 'svg'): Promise<Blob> {
    return blobFromBase64(this.wasm.Report(JSON.stringify(model), format), format === 'svg' ? 'image/svg+xml' : 'text/html');
  }

  public async merge(base: TmForgeModel | null, ours: TmForgeModel, theirs: TmForgeModel): Promise<MergeResult> {
    const dto = JSON.parse(
      this.wasm.Merge(base ? JSON.stringify(base) : '', JSON.stringify(ours), JSON.stringify(theirs)),
    ) as components['schemas']['MergeResultDto'];
    return toMergeResult(dto);
  }
}

/** The honest offline fallback client (client-side authoring only). */
export const offlineEngine: IEngineClient = new OfflineEngineClient();

/** Creates a client bound to the real engine API. */
export function createHttpEngine(baseUrl: string = ENGINE_BASE_URL): IEngineClient {
  return new HttpEngineClient(baseUrl);
}

/** Minimal typing for the .NET WASM bootstrap module (`_framework/dotnet.js`). */
interface DotnetBootstrap {
  dotnet: {
    create(): Promise<{
      getConfig(): { mainAssemblyName: string };
      getAssemblyExports(assemblyName: string): Promise<Record<string, Record<string, Record<string, WasmEngineExports>>>>;
    }>;
  };
}

let wasmEnginePromise: Promise<IEngineClient | null> | undefined;

/**
 * Lazily loads the in-browser (.NET WebAssembly) engine and returns a client bound to it, or `null`
 * when the runtime can't load (WebAssembly disabled, blocked by a Content-Security-Policy, the bundle
 * isn't staged, offline before it's cached, etc.). Cached, so the multi-MB runtime loads at most once.
 */
export function loadWasmEngine(baseUrl: string = import.meta.env.BASE_URL): Promise<IEngineClient | null> {
  wasmEnginePromise ??= (async (): Promise<IEngineClient | null> => {
    try {
      const url = `${baseUrl}wasm/_framework/dotnet.js`;
      const mod = (await import(/* @vite-ignore */ url)) as DotnetBootstrap;
      const runtime = await mod.dotnet.create();
      const exports = await runtime.getAssemblyExports(runtime.getConfig().mainAssemblyName);
      const wasm = exports?.ThreatModelForge?.Wasm?.Engine;
      return wasm ? new WasmEngineClient(wasm) : null;
    } catch {
      return null;
    }
  })();
  return wasmEnginePromise;
}

/** Returns true when the engine API answers its health probe. */
export async function probeEngine(baseUrl: string = ENGINE_BASE_URL): Promise<boolean> {
  try {
    const client = createClient<paths>({ baseUrl });
    const { response } = await client.GET('/v1/health');
    return response.ok;
  } catch {
    return false;
  }
}
