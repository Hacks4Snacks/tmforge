import { useState } from 'react';
import type { DfdEdge, DfdNode } from './types';
import type { PropertyDescriptorInfo, StencilInfo } from './engineClient';

const KIND_LABEL: Record<string, string> = {
  process: 'Process',
  datastore: 'Data store',
  external: 'External entity',
  boundary: 'Trust boundary',
};

interface InspectorProps {
  node: DfdNode | null;
  edge: DfdEdge | null;
  /** The stencil catalog, so a specialized node can show its stencil identity. */
  stencils: StencilInfo[];
  /** The typed property schema, so element properties render as dropdowns/checkboxes with canonical values. */
  propertySchema: PropertyDescriptorInfo[];
  /** Called when a name edit begins, so a single undo step covers the whole edit. */
  onBeginNameEdit: () => void;
  onRenameNode: (id: string, label: string) => void;
  onRenameEdge: (id: string, label: string) => void;
  /** Sets (or, when value is empty, clears) a flow custom property such as Protocol or DataType. */
  onSetEdgeProperty: (id: string, key: string, value: string) => void;
  /** Adds or updates an element custom property. */
  onSetNodeProperty: (id: string, key: string, value: string) => void;
  /** Removes an element custom property. */
  onRemoveNodeProperty: (id: string, key: string) => void;
  onDelete: () => void;
}

/**
 * The schema-driven property editor shared by elements and data flows. It renders a typed control
 * for every property the engine's schema declares for the selected primitive — so every property an
 * analysis rule can read is reachable — followed by any custom (non-schema) properties and a
 * free-form add row. Enum and boolean properties render as dropdowns of canonical values; string
 * properties render as text inputs. Selecting "(none)" or clearing a value removes the property.
 */
function PropertyFields(props: {
  /** The DFD primitive whose schema to render: 'process' | 'datastore' | 'external' | 'flow'. */
  appliesTo: string;
  /** The custom properties currently set on the element or flow. */
  properties: Record<string, string>;
  /** The full typed property schema; filtered here by the primitive it applies to. */
  schema: PropertyDescriptorInfo[];
  /** Adds or updates a property. */
  onSet: (key: string, value: string) => void;
  /** Removes a property. */
  onRemove: (key: string) => void;
  /** Called when a free-text edit begins, so one undo step covers the whole edit. */
  onBeginEdit: () => void;
}) {
  const { appliesTo, properties, schema, onSet, onRemove, onBeginEdit } = props;
  const [newKey, setNewKey] = useState('');
  const [newValue, setNewValue] = useState('');

  const schemaFor = schema.filter((descriptor) => descriptor.appliesTo === appliesTo);
  const schemaNames = new Set(schemaFor.map((descriptor) => descriptor.name));
  const custom = Object.entries(properties).filter(([key]) => !schemaNames.has(key));

  const typedControl = (descriptor: PropertyDescriptorInfo) => {
    const value = properties[descriptor.name] ?? '';
    const commit = (next: string) => (next ? onSet(descriptor.name, next) : onRemove(descriptor.name));
    if (descriptor.kind === 'enum' || descriptor.kind === 'bool') {
      return (
        <select value={value} onChange={(event) => commit(event.target.value)}>
          <option value="">(none)</option>
          {descriptor.values.map((option) => (
            <option key={option} value={option}>
              {option}
            </option>
          ))}
        </select>
      );
    }
    return <input value={value} onFocus={onBeginEdit} onChange={(event) => commit(event.target.value)} />;
  };

  return (
    <div className="inspector-props">
      <span className="inspector-props-title">Properties</span>
      {schemaFor.length === 0 && (
        <p className="inspector-hint">
          Typed properties load from the analysis engine. Add properties by name below (for example, Protocol or
          DataType).
        </p>
      )}
      {schemaFor.map((descriptor) => (
        <label className="inspector-field" key={descriptor.name}>
          <span>{descriptor.name}</span>
          {typedControl(descriptor)}
        </label>
      ))}
      {custom.length > 0 && (
        <>
          <span className="inspector-props-title">Custom properties</span>
          {custom.map(([key, value]) => (
            <div className="inspector-prop-row" key={key}>
              <span className="inspector-prop-key" title={key}>
                {key}
              </span>
              <input value={value} onFocus={onBeginEdit} onChange={(event) => onSet(key, event.target.value)} />
              <button className="inspector-prop-del" title={`Remove ${key}`} onClick={() => onRemove(key)}>
                ×
              </button>
            </div>
          ))}
        </>
      )}
      <div className="inspector-prop-add">
        <input placeholder="key" value={newKey} onChange={(event) => setNewKey(event.target.value)} />
        <input placeholder="value" value={newValue} onChange={(event) => setNewValue(event.target.value)} />
        <button
          className="btn"
          disabled={!newKey.trim()}
          onClick={() => {
            const key = newKey.trim();
            onSet(key, newValue);
            setNewKey('');
            setNewValue('');
          }}
        >
          Add
        </button>
      </div>
    </div>
  );
}

export function Inspector(props: InspectorProps) {
  const { node, edge } = props;

  if (!node && !edge) {
    return (
      <aside className="inspector">
        <h2 className="inspector-title">Inspector</h2>
        <p className="inspector-empty">Select an element or data flow to edit its name and properties.</p>
      </aside>
    );
  }

  if (edge) {
    const label = typeof edge.label === 'string' ? edge.label : '';
    return (
      <aside className="inspector">
        <h2 className="inspector-title">Data flow</h2>
        <label className="inspector-field">
          <span>Label</span>
          <input
            value={label}
            onFocus={props.onBeginNameEdit}
            onChange={(event) => props.onRenameEdge(edge.id, event.target.value)}
          />
        </label>
        <PropertyFields
          key={edge.id}
          appliesTo="flow"
          properties={edge.data?.properties ?? {}}
          schema={props.propertySchema}
          onSet={(key, value) => props.onSetEdgeProperty(edge.id, key, value)}
          onRemove={(key) => props.onSetEdgeProperty(edge.id, key, '')}
          onBeginEdit={props.onBeginNameEdit}
        />
        <p className="inspector-hint">
          Set the properties a rule reads — for example <b>Protocol</b> and <b>Port</b>, a <b>DataType</b>, or an{' '}
          <b>Algorithm</b> — and mention the protocol in the label, then re-validate to clear the flow findings
          (for example TM1008 / TM1009 / TM1010 / TM1013 / TM1016 / TM1025).
        </p>
        <button className="btn btn-danger" onClick={props.onDelete}>
          Delete flow
        </button>
      </aside>
    );
  }

  if (!node) {
    return null;
  }

  const stencil = node.data.stencilType ? props.stencils.find((s) => s.id === node.data.stencilType) : undefined;
  const baseKind = node.type ?? 'process';
  return (
    <aside className="inspector">
      <h2 className="inspector-title">{stencil?.label ?? KIND_LABEL[node.type ?? 'process'] ?? 'Element'}</h2>
      {stencil && (
        <p className="inspector-type">
          {stencil.category} · {KIND_LABEL[stencil.base] ?? stencil.base}
        </p>
      )}
      <label className="inspector-field">
        <span>Name</span>
        <input
          value={node.data.label}
          onFocus={props.onBeginNameEdit}
          onChange={(event) => props.onRenameNode(node.id, event.target.value)}
        />
      </label>
      <PropertyFields
        key={node.id}
        appliesTo={baseKind}
        properties={node.data.properties ?? {}}
        schema={props.propertySchema}
        onSet={(key, value) => props.onSetNodeProperty(node.id, key, value)}
        onRemove={(key) => props.onRemoveNodeProperty(node.id, key)}
        onBeginEdit={props.onBeginNameEdit}
      />
      <button className="btn btn-danger" onClick={props.onDelete}>
        Delete element
      </button>
    </aside>
  );
}
