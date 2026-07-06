import { useState } from 'react';
import type { DfdEdge, DfdNode } from './types';
import type { PropertyDescriptorInfo, StencilInfo } from './engineClient';

const PROTOCOLS = ['HTTPS', 'HTTP', 'TCP', 'UDP', 'gRPC', 'AMQP', 'MQTT'];
const DATA_TAGS = ['EUII', 'EUPI', 'System Metadata', 'Customer Content', 'Account Data', 'OII', 'Access Control Data'];
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

export function Inspector(props: InspectorProps) {
  const { node, edge } = props;
  const [newKey, setNewKey] = useState('');
  const [newValue, setNewValue] = useState('');

  if (!node && !edge) {
    return (
      <aside className="inspector">
        <h2 className="inspector-title">Inspector</h2>
        <p className="inspector-empty">Select an element or data flow to edit its name and properties.</p>
      </aside>
    );
  }

  if (edge) {
    const flowProps = edge.data?.properties ?? {};
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
        <label className="inspector-field">
          <span>Protocol</span>
          <select value={flowProps.Protocol ?? ''} onChange={(event) => props.onSetEdgeProperty(edge.id, 'Protocol', event.target.value)}>
            <option value="">(none)</option>
            {PROTOCOLS.map((protocol) => (
              <option key={protocol} value={protocol}>
                {protocol}
              </option>
            ))}
          </select>
        </label>
        <label className="inspector-field">
          <span>Data classification</span>
          <select value={flowProps.DataType ?? ''} onChange={(event) => props.onSetEdgeProperty(edge.id, 'DataType', event.target.value)}>
            <option value="">(none)</option>
            {DATA_TAGS.map((tag) => (
              <option key={tag} value={tag}>
                {tag}
              </option>
            ))}
          </select>
        </label>
        <p className="inspector-hint">
          Set <b>Protocol</b> (for example HTTPS) and a <b>Data classification</b>, and mention the protocol in the
          label, then re-validate to clear the TM1008 / TM1010 / TM1013 / TM1009 findings.
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
  const nodeProps = Object.entries(node.data.properties ?? {});
  const baseKind = node.type ?? 'process';
  const schemaFor = props.propertySchema.filter((descriptor) => descriptor.appliesTo === baseKind);
  const byName = new Map(schemaFor.map((descriptor) => [descriptor.name, descriptor]));
  const knownUnset = schemaFor.filter((descriptor) => !(descriptor.name in (node.data.properties ?? {})));
  const valueControl = (key: string, value: string) => {
    const descriptor = byName.get(key);
    if (descriptor && (descriptor.kind === 'enum' || descriptor.kind === 'bool')) {
      return (
        <select value={value} onChange={(event) => props.onSetNodeProperty(node.id, key, event.target.value)}>
          <option value="">(none)</option>
          {descriptor.values.map((option) => (
            <option key={option} value={option}>
              {option}
            </option>
          ))}
        </select>
      );
    }
    return (
      <input
        value={value}
        onFocus={props.onBeginNameEdit}
        onChange={(event) => props.onSetNodeProperty(node.id, key, event.target.value)}
      />
    );
  };
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
      <div className="inspector-props">
        <span className="inspector-props-title">Properties</span>
        {nodeProps.length === 0 ? (
          <p className="inspector-hint">No custom properties yet. Add one below (for example, Encryption or DataType).</p>
        ) : (
          nodeProps.map(([key, value]) => (
            <div className="inspector-prop-row" key={key}>
              <span className="inspector-prop-key" title={key}>
                {key}
              </span>
              {valueControl(key, value)}
              <button
                className="inspector-prop-del"
                title={`Remove ${key}`}
                onClick={() => props.onRemoveNodeProperty(node.id, key)}
              >
                ×
              </button>
            </div>
          ))
        )}
        <div className="inspector-prop-add">
          <input placeholder="key" list="tmf-known-props" value={newKey} onChange={(event) => setNewKey(event.target.value)} />
          <datalist id="tmf-known-props">
            {knownUnset.map((descriptor) => (
              <option key={descriptor.name} value={descriptor.name} />
            ))}
          </datalist>
          <input placeholder="value" value={newValue} onChange={(event) => setNewValue(event.target.value)} />
          <button
            className="btn"
            disabled={!newKey.trim()}
            onClick={() => {
              const key = newKey.trim();
              const descriptor = byName.get(key);
              props.onSetNodeProperty(node.id, key, newValue || descriptor?.default || '');
              setNewKey('');
              setNewValue('');
            }}
          >
            Add
          </button>
        </div>
      </div>
      <button className="btn btn-danger" onClick={props.onDelete}>
        Delete element
      </button>
    </aside>
  );
}
