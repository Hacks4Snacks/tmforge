import { useEffect, useRef, type RefObject } from 'react';
import type { ModelOutline, OutlineFlow, OutlineGroup, OutlineOrder } from './outline';

/** Short kind labels for the object rows. */
const KIND_LABEL: Record<string, string> = {
  process: 'Process',
  datastore: 'Data store',
  external: 'External',
};

/** How the outline can be ordered, in the order the control offers them. */
const ORDER_OPTIONS: readonly { value: OutlineOrder; label: string; hint: string }[] = [
  { value: 'model', label: 'Model order', hint: 'List flows and objects in the order the model stores them' },
  { value: 'name', label: 'Name order', hint: 'List flows and objects by name, with numbers compared as numbers' },
];

/**
 * The review outline: every flow on the page as a numbered, ordered list, and every object grouped
 * under the trust boundary it sits in.
 *
 * On a large diagram the drawing order is whatever fitted on the canvas, so a reviewer cannot tell
 * which flow follows which, or which objects share a boundary, without tracing lines by eye. This
 * states both directly, and each row jumps the canvas to the thing it names — including the
 * previous/next stepper, which walks the flows one at a time so a review can be worked through in
 * order rather than hunted for.
 */
export function ModelOutline({
  outline,
  flows,
  order,
  onOrderChange,
  crossingOnly,
  onCrossingOnlyChange,
  selectedFlowId,
  selectedObjectId,
  onSelectFlow,
  onSelectObject,
  onStep,
}: {
  outline: ModelOutline;
  /** The flows to list — the outline's flows, less any hidden by the boundary-crossing filter. */
  flows: OutlineFlow[];
  order: OutlineOrder;
  onOrderChange: (order: OutlineOrder) => void;
  crossingOnly: boolean;
  onCrossingOnlyChange: (value: boolean) => void;
  selectedFlowId: string | null;
  selectedObjectId: string | null;
  onSelectFlow: (id: string) => void;
  onSelectObject: (id: string) => void;
  onStep: (delta: number) => void;
}) {
  const index = flows.findIndex((flow) => flow.id === selectedFlowId);
  const canStep = flows.length > 0;

  // Follow the selection down the list, so stepping through a long model never leaves the current
  // flow scrolled out of the panel. (`scrollIntoView` is absent in jsdom, hence the optional call.)
  const selectedRow = useRef<HTMLButtonElement | null>(null);
  useEffect(() => {
    selectedRow.current?.scrollIntoView?.({ block: 'nearest' });
  }, [selectedFlowId]);

  const selectedObjectRow = useRef<HTMLButtonElement | null>(null);
  useEffect(() => {
    selectedObjectRow.current?.scrollIntoView?.({ block: 'nearest' });
  }, [selectedObjectId]);

  return (
    <div className="outline">
      <div className="outline-controls">
        <select
          className="outline-order"
          value={order}
          aria-label="Outline order"
          onChange={(event) => onOrderChange(event.target.value as OutlineOrder)}
          title={ORDER_OPTIONS.find((option) => option.value === order)?.hint}
        >
          {ORDER_OPTIONS.map((option) => (
            <option key={option.value} value={option.value}>
              {option.label}
            </option>
          ))}
        </select>
        <label className="outline-filter">
          <input type="checkbox" checked={crossingOnly} onChange={(event) => onCrossingOnlyChange(event.target.checked)} />
          Boundary-crossing only
        </label>
      </div>

      <div className="outline-section">
        <div className="outline-head">
          <span className="outline-head-title">
            Flows
            <span className="outline-count">
              {crossingOnly ? `${flows.length} of ${outline.flows.length}` : outline.flows.length}
            </span>
          </span>
          <span className="outline-step">
            <button type="button" onClick={() => onStep(-1)} disabled={!canStep} aria-label="Previous flow" title="Previous flow">
              ◂
            </button>
            <span className="outline-step-count">
              {index >= 0 ? index + 1 : '–'}/{flows.length}
            </span>
            <button type="button" onClick={() => onStep(1)} disabled={!canStep} aria-label="Next flow" title="Next flow">
              ▸
            </button>
          </span>
        </div>
        {flows.length === 0 ? (
          <p className="outline-empty">{outline.flows.length === 0 ? 'No flows on this page.' : 'No flow crosses a trust boundary.'}</p>
        ) : (
          <ul className="outline-list">
            {flows.map((flow) => (
              <li key={flow.id}>
                <button
                  type="button"
                  className={`outline-row${flow.id === selectedFlowId ? ' selected' : ''}`}
                  ref={flow.id === selectedFlowId ? selectedRow : undefined}
                  onClick={() => onSelectFlow(flow.id)}
                >
                  <span className="outline-pos">{flow.position}</span>
                  <span className="outline-body">
                    <span className="outline-name">{flow.name}</span>
                    <span className="outline-meta">
                      {flow.source} → {flow.target}
                    </span>
                    {flow.crossings.length > 0 ? (
                      <span className="outline-crossing" title={`Crosses ${flow.crossings.join(', ')}`}>
                        crosses {flow.crossings.join(' · ')}
                      </span>
                    ) : null}
                  </span>
                </button>
              </li>
            ))}
          </ul>
        )}
      </div>

      <div className="outline-section">
        <div className="outline-head">
          <span className="outline-head-title">
            Objects
            <span className="outline-count">{outline.groups.reduce((total, group) => total + group.objects.length, 0)}</span>
          </span>
        </div>
        {outline.groups.length === 0 ? (
          <p className="outline-empty">No objects on this page.</p>
        ) : (
          outline.groups.map((group) => (
            <OutlineBoundary
              key={group.id || 'unbounded'}
              group={group}
              selectedObjectId={selectedObjectId}
              selectedRef={selectedObjectRow}
              onSelectObject={onSelectObject}
            />
          ))
        )}
      </div>
    </div>
  );
}

/** One trust boundary and the objects listed under it. */
function OutlineBoundary({
  group,
  selectedObjectId,
  selectedRef,
  onSelectObject,
}: {
  group: OutlineGroup;
  selectedObjectId: string | null;
  selectedRef: RefObject<HTMLButtonElement | null>;
  onSelectObject: (id: string) => void;
}) {
  return (
    <div className="outline-group">
      <button
        type="button"
        className={`outline-group-head${group.id === selectedObjectId ? ' selected' : ''}`}
        ref={group.id === selectedObjectId ? selectedRef : undefined}
        onClick={() => (group.id ? onSelectObject(group.id) : undefined)}
        disabled={!group.id}
      >
        <span className="outline-dot kind-boundary" aria-hidden />
        <span className="outline-name">{group.name}</span>
        <span className="outline-count">{group.objects.length}</span>
      </button>
      {group.objects.length === 0 ? (
        <p className="outline-empty">Empty</p>
      ) : (
        <ul className="outline-list">
          {group.objects.map((object) => (
            <li key={object.id}>
              <button
                type="button"
                className={`outline-row${object.id === selectedObjectId ? ' selected' : ''}`}
                ref={object.id === selectedObjectId ? selectedRef : undefined}
                onClick={() => onSelectObject(object.id)}
              >
                <span className={`outline-dot kind-${object.kind}`} aria-hidden />
                <span className="outline-body">
                  <span className="outline-name">{object.name}</span>
                  <span className="outline-meta">
                    {KIND_LABEL[object.kind] ?? object.kind} · {object.flowCount === 1 ? '1 flow' : `${object.flowCount} flows`}
                  </span>
                </span>
              </button>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
