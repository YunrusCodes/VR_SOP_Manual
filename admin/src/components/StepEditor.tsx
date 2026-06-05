import { useSortable } from '@dnd-kit/sortable';
import { CSS } from '@dnd-kit/utilities';
import { useState } from 'react';

import { blankException } from '../courseDraft';
import type { Media, StepDraft } from '../types';
import { ExceptionEditor } from './ExceptionEditor';

interface Props {
  step: StepDraft;
  positionIndex: number;
  allSteps: StepDraft[];
  availableImages: string[];
  availableVideos: string[];
  imageUrl: (filename: string) => string;
  onChange: (next: StepDraft) => void;
  onInsertAbove: () => void;
  onDelete: () => void;
}

export function StepEditor({
  step,
  positionIndex,
  allSteps,
  availableImages,
  availableVideos,
  imageUrl,
  onChange,
  onInsertAbove,
  onDelete,
}: Props) {
  const { attributes, listeners, setNodeRef, transform, transition, isDragging } = useSortable({
    id: step._id,
  });
  const [open, setOpen] = useState(false);

  const style = {
    transform: CSS.Transform.toString(transform),
    transition,
    opacity: isDragging ? 0.4 : 1,
  };

  function patch<K extends keyof StepDraft>(key: K, value: StepDraft[K]) {
    onChange({ ...step, [key]: value });
  }

  function patchMedia<K extends keyof Media>(key: K, value: Media[K]) {
    onChange({ ...step, media: { ...step.media, [key]: value } });
  }

  function addException() {
    if (step.exceptions.length >= 3) return;
    onChange({ ...step, exceptions: [...step.exceptions, blankException()] });
  }

  return (
    <li ref={setNodeRef} style={style} className="rounded border border-slate-200 bg-white">
      <div className="flex items-center gap-2 px-3 py-2">
        <button
          {...attributes}
          {...listeners}
          className="cursor-grab text-slate-400 hover:text-slate-600 px-1"
          title="拖曳排序"
        >
          ⋮⋮
        </button>
        <span className="font-mono text-xs text-slate-500 w-8 text-center">
          {(positionIndex + 1).toString().padStart(2, '0')}
        </span>
        <input
          value={step.name}
          onChange={(e) => patch('name', e.target.value)}
          placeholder="步驟名稱"
          className="flex-1 rounded border border-slate-300 px-2 py-1 text-sm"
        />
        <button
          onClick={() => setOpen((v) => !v)}
          className="text-xs text-slate-500 hover:text-slate-700"
        >
          {open ? '收合' : '展開'}
        </button>
        <button
          onClick={onInsertAbove}
          className="text-xs text-blue-600 hover:text-blue-700"
          title="於此步驟之前插入新步驟"
        >
          + 上面
        </button>
        <button
          onClick={onDelete}
          className="text-xs text-rose-600 hover:text-rose-700"
          title="刪除此步驟"
        >
          刪除
        </button>
      </div>

      {open && (
        <div className="border-t border-slate-100 px-3 py-3 space-y-3">
          <div className="grid grid-cols-2 gap-3">
            <label className="block">
              <span className="text-xs font-medium text-slate-600">主標題</span>
              <input
                value={step.mainTitle}
                onChange={(e) => patch('mainTitle', e.target.value)}
                className="mt-1 block w-full rounded border border-slate-300 px-2 py-1 text-sm"
              />
            </label>
            <label className="block">
              <span className="text-xs font-medium text-slate-600">副標題</span>
              <input
                value={step.subTitle}
                onChange={(e) => patch('subTitle', e.target.value)}
                className="mt-1 block w-full rounded border border-slate-300 px-2 py-1 text-sm"
              />
            </label>
          </div>

          <label className="block">
            <span className="text-xs font-medium text-slate-600">描述</span>
            <textarea
              value={step.description}
              onChange={(e) => patch('description', e.target.value)}
              rows={6}
              className="mt-1 block w-full rounded border border-slate-300 px-2 py-1 text-sm font-sans"
            />
          </label>

          <div className="flex gap-3">
            <label className="block flex-1">
              <span className="text-xs font-medium text-slate-600">媒體類型</span>
              <select
                value={step.media.kind}
                onChange={(e) =>
                  patchMedia('kind', e.target.value as Media['kind'])
                }
                className="mt-1 block w-full rounded border border-slate-300 bg-white px-2 py-1 text-sm"
              >
                <option value="none">無</option>
                <option value="image">圖片</option>
                <option value="video">影片</option>
              </select>
            </label>
            <label className="block flex-[2]">
              <span className="text-xs font-medium text-slate-600">媒體檔名</span>
              <input
                list={`media-list-${step._id}`}
                value={step.media.filename ?? ''}
                onChange={(e) => patchMedia('filename', e.target.value || null)}
                disabled={step.media.kind === 'none'}
                placeholder={
                  step.media.kind === 'none'
                    ? '—'
                    : step.media.kind === 'image'
                      ? 'sun.jpg'
                      : 'mitosis.mp4'
                }
                className="mt-1 block w-full rounded border border-slate-300 px-2 py-1 text-sm disabled:bg-slate-100"
              />
              <datalist id={`media-list-${step._id}`}>
                {(step.media.kind === 'image' ? availableImages : availableVideos).map((f) => (
                  <option key={f} value={f} />
                ))}
              </datalist>
            </label>
            {step.media.kind === 'image' && step.media.filename && (
              <div className="mt-5 h-16 w-16 flex-shrink-0 overflow-hidden rounded border border-slate-200 bg-white">
                <img
                  src={imageUrl(step.media.filename)}
                  alt={step.media.filename}
                  className="h-full w-full object-cover"
                  onError={(e) => ((e.target as HTMLImageElement).style.opacity = '0.2')}
                />
              </div>
            )}
          </div>

          <label className="block">
            <span className="text-xs font-medium text-slate-600">下一步提示</span>
            <input
              value={step.nextStepIndication}
              onChange={(e) => patch('nextStepIndication', e.target.value)}
              className="mt-1 block w-full rounded border border-slate-300 px-2 py-1 text-sm"
            />
          </label>

          <div className="space-y-2">
            <div className="flex items-center justify-between">
              <span className="text-xs font-medium text-slate-600">
                例外選項 ({step.exceptions.length}/3)
              </span>
              <button
                onClick={addException}
                disabled={step.exceptions.length >= 3}
                className="text-xs text-emerald-600 hover:text-emerald-700 disabled:opacity-40"
              >
                + 新增
              </button>
            </div>
            {step.exceptions.map((ex) => (
              <ExceptionEditor
                key={ex._id}
                steps={allSteps}
                selfId={step._id}
                exception={ex}
                onChange={(next) =>
                  patch(
                    'exceptions',
                    step.exceptions.map((x) => (x._id === ex._id ? next : x)),
                  )
                }
                onRemove={() =>
                  patch(
                    'exceptions',
                    step.exceptions.filter((x) => x._id !== ex._id),
                  )
                }
              />
            ))}
          </div>
        </div>
      )}
    </li>
  );
}
