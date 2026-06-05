// Conversion between the wire-format Course (orders are integer references) and
// the editor's CourseDraft (stable client-side ids so we don't lose the meaning
// of "go to step N" when N is renumbered after a reorder).

import type {
  Course,
  CourseDraft,
  ExceptionOption,
  ExceptionOptionDraft,
  Step,
  StepDraft,
} from './types';

function rid(): string {
  if (typeof crypto !== 'undefined' && typeof crypto.randomUUID === 'function') {
    return crypto.randomUUID();
  }
  return `id-${Math.random().toString(36).slice(2, 10)}-${Date.now().toString(36)}`;
}

export function toDraft(course: Course): CourseDraft {
  const steps: StepDraft[] = course.steps.map((s) => ({
    ...s,
    _id: rid(),
    exceptions: s.exceptions.map<ExceptionOptionDraft>((ex) => ({
      _id: rid(),
      label: ex.label,
      action:
        ex.action.type === 'goToStep'
          ? { type: 'goToStep', targetId: null, step: ex.action.step ?? undefined }
          : { type: 'showMessage', text: ex.action.text ?? '' },
    })),
  }));
  // Second pass: resolve goToStep step numbers to draft step ids.
  const byOrder = new Map<number, StepDraft>();
  steps.forEach((s) => byOrder.set(s.order, s));
  for (const s of steps) {
    for (const ex of s.exceptions) {
      if (ex.action.type === 'goToStep' && ex.action.step != null) {
        const target = byOrder.get(ex.action.step);
        ex.action.targetId = target ? target._id : null;
      }
    }
  }
  return {
    name: course.name,
    displayName: course.displayName,
    introduction: course.introduction,
    steps,
  };
}

export function fromDraft(draft: CourseDraft): Course {
  // Renumber so step.order matches its position 1..N. Then resolve goToStep
  // targetId back to the target's new order.
  const renumbered = draft.steps.map((s, i) => ({ ...s, order: i + 1 }));
  const idToOrder = new Map<string, number>();
  renumbered.forEach((s) => idToOrder.set(s._id, s.order));
  const steps: Step[] = renumbered.map((s) => ({
    order: s.order,
    mainTitle: s.mainTitle,
    subTitle: s.subTitle,
    name: s.name,
    description: s.description,
    media: { ...s.media },
    nextStepIndication: s.nextStepIndication,
    exceptions: s.exceptions
      .map<ExceptionOption | null>((ex) => {
        if (!ex.label.trim()) return null;
        if (ex.action.type === 'goToStep') {
          const order = ex.action.targetId ? idToOrder.get(ex.action.targetId) : undefined;
          if (order == null) return null;
          return { label: ex.label, action: { type: 'goToStep', step: order } };
        }
        return { label: ex.label, action: { type: 'showMessage', text: ex.action.text } };
      })
      .filter((x): x is ExceptionOption => x != null),
  }));
  return {
    name: draft.name,
    displayName: draft.displayName,
    introduction: draft.introduction,
    steps,
  };
}

export function blankStep(): StepDraft {
  return {
    _id: rid(),
    order: 0, // renumbered on save
    mainTitle: '',
    subTitle: '',
    name: '',
    description: '',
    media: { kind: 'none', filename: null },
    nextStepIndication: '',
    exceptions: [],
  };
}

export function blankException(): ExceptionOptionDraft {
  return {
    _id: rid(),
    label: '',
    action: { type: 'showMessage', text: '' },
  };
}
