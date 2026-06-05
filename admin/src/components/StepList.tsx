import {
  DndContext,
  PointerSensor,
  closestCenter,
  useSensor,
  useSensors,
  type DragEndEvent,
} from '@dnd-kit/core';
import {
  SortableContext,
  arrayMove,
  verticalListSortingStrategy,
} from '@dnd-kit/sortable';

import { blankStep } from '../courseDraft';
import type { StepDraft } from '../types';
import { StepEditor } from './StepEditor';

interface Props {
  steps: StepDraft[];
  availableImages: string[];
  availableVideos: string[];
  imageUrl: (filename: string) => string;
  onChange: (next: StepDraft[]) => void;
}

export function StepList({
  steps,
  availableImages,
  availableVideos,
  imageUrl,
  onChange,
}: Props) {
  const sensors = useSensors(
    useSensor(PointerSensor, { activationConstraint: { distance: 4 } }),
  );

  function handleDragEnd(event: DragEndEvent) {
    const { active, over } = event;
    if (!over || active.id === over.id) return;
    const from = steps.findIndex((s) => s._id === active.id);
    const to = steps.findIndex((s) => s._id === over.id);
    if (from < 0 || to < 0) return;
    onChange(arrayMove(steps, from, to));
  }

  function insertAt(index: number) {
    const next = [...steps];
    next.splice(index, 0, blankStep());
    onChange(next);
  }

  return (
    <div className="space-y-2">
      <DndContext sensors={sensors} collisionDetection={closestCenter} onDragEnd={handleDragEnd}>
        <SortableContext items={steps.map((s) => s._id)} strategy={verticalListSortingStrategy}>
          <ul className="space-y-2">
            {steps.map((s, idx) => (
              <StepEditor
                key={s._id}
                step={s}
                positionIndex={idx}
                allSteps={steps}
                availableImages={availableImages}
                availableVideos={availableVideos}
                imageUrl={imageUrl}
                onChange={(next) =>
                  onChange(steps.map((x) => (x._id === s._id ? next : x)))
                }
                onInsertAbove={() => insertAt(idx)}
                onDelete={() => onChange(steps.filter((x) => x._id !== s._id))}
              />
            ))}
          </ul>
        </SortableContext>
      </DndContext>

      <button
        onClick={() => insertAt(steps.length)}
        className="w-full rounded border border-dashed border-slate-300 py-2 text-sm text-slate-500 hover:border-slate-400 hover:text-slate-700"
      >
        + 在最後加一步
      </button>
    </div>
  );
}
