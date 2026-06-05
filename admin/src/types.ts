// Mirror of api/schema.py — keep aligned by hand for now; we can later generate
// from OpenAPI once the surface grows.

export type MediaKind = 'image' | 'video' | 'none';

export interface Media {
  kind: MediaKind;
  filename: string | null;
}

export type ExceptionAction =
  | { type: 'goToStep'; step: number; text?: null }
  | { type: 'showMessage'; text: string; step?: null };

export interface ExceptionOption {
  label: string;
  action: ExceptionAction;
}

export interface Step {
  order: number;
  mainTitle: string;
  subTitle: string;
  name: string;
  description: string;
  media: Media;
  nextStepIndication: string;
  exceptions: ExceptionOption[];
}

export interface Course {
  name: string;
  displayName: string;
  introduction: string;
  steps: Step[];
}

// Internal UI-only mirror of Step/Course that carries a stable client-side id
// so a goToStep target survives reorder/insert/delete (its order index changes
// but its identity doesn't).
export interface StepDraft {
  _id: string;
  order: number;
  mainTitle: string;
  subTitle: string;
  name: string;
  description: string;
  media: Media;
  nextStepIndication: string;
  exceptions: ExceptionOptionDraft[];
}

export interface ExceptionOptionDraft {
  _id: string;
  label: string;
  action: ExceptionActionDraft;
}

export type ExceptionActionDraft =
  | { type: 'goToStep'; targetId: string | null; step?: number }
  | { type: 'showMessage'; text: string };

export interface CourseDraft {
  name: string;
  displayName: string;
  introduction: string;
  steps: StepDraft[];
}

export interface CourseSummary {
  name: string;
  displayName: string;
}

export interface ApiError {
  error: { code: string; message: string };
}
