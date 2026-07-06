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
 * Two implementations ship here:
 *   - `StubEngineClient`  → offline canned rules (works with no backend).
 *   - `HttpEngineClient`  → calls the real .NET engine over the `/v1` API. In
 *                           production this transport is also how a localhost sidecar or a
 *                           WASM-hosted engine would be reached; only the base URL changes.
 *
 * `read`/`write` just (de)serialize the canonical `tmforge-json` client-side — no engine needed —
 * so both clients share them. Everything else (`validate`, `getFormats`, `detect`, `readFile`,
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

class StubEngineClient implements IEngineClient {
  public readonly label = 'stub (offline)';

  public write(model: TmForgeModel): Promise<string> {
    return writeJson(model);
  }

  public read(text: string): Promise<TmForgeModel> {
    return readJson(text);
  }

  public async validate(model: TmForgeModel): Promise<Finding[]> {
    const findings: Finding[] = [];
    const connected = new Set<string>();

    for (const flow of model.flows) {
      connected.add(flow.source);
      connected.add(flow.target);
      const name = flow.name.trim().toLowerCase();
      if (name === '' || name === 'data flow') {
        findings.push({
          id: `unlabeled:${flow.id}`,
          severity: 'info',
          message: 'Data flow has no descriptive label.',
          elementIds: [flow.id],
        });
      }
    }

    for (const el of model.elements) {
      if (el.kind === 'boundary') {
        continue;
      }
      if (!connected.has(el.id)) {
        findings.push({
          id: `isolated:${el.id}`,
          severity: 'warning',
          message: `"${el.name}" has no data flows.`,
          elementIds: [el.id],
        });
      }
    }

    if (!model.elements.some((el) => el.kind === 'boundary')) {
      findings.push({
        id: 'no-boundary',
        severity: 'info',
        message: 'No trust boundary defined — every flow is implicitly in one zone.',
        elementIds: [],
      });
    }

    return findings;
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
}

/** The offline fallback client. */
export const stubEngine: IEngineClient = new StubEngineClient();

/** Creates a client bound to the real engine API. */
export function createHttpEngine(baseUrl: string = ENGINE_BASE_URL): IEngineClient {
  return new HttpEngineClient(baseUrl);
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
