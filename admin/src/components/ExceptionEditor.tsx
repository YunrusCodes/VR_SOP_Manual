import type {
  ExceptionActionDraft,
  ExceptionOptionDraft,
  StepDraft,
} from '../types';

interface Props {
  steps: StepDraft[];
  selfId: string;
  exception: ExceptionOptionDraft;
  onChange: (next: ExceptionOptionDraft) => void;
  onRemove: () => void;
}

export function ExceptionEditor({ steps, selfId, exception, onChange, onRemove }: Props) {
  function setAction(action: ExceptionActionDraft) {
    onChange({ ...exception, action });
  }

  return (
    <div className="rounded border border-slate-200 bg-slate-50 p-2 space-y-2">
      <div className="flex gap-2 items-center">
        <input
          className="flex-1 rounded border border-slate-300 bg-white px-2 py-1 text-sm"
          value={exception.label}
          onChange={(e) => onChange({ ...exception, label: e.target.value })}
          placeholder="按鈕標籤（例如「日冕是什麼」）"
        />
        <button
          onClick={onRemove}
          className="text-rose-600 hover:text-rose-700 text-xs"
          title="移除"
        >
          移除
        </button>
      </div>

      <div className="flex gap-3 text-xs">
        <label className="flex items-center gap-1">
          <input
            type="radio"
            checked={exception.action.type === 'goToStep'}
            onChange={() =>
              setAction({
                type: 'goToStep',
                targetId: exception.action.type === 'goToStep' ? exception.action.targetId : null,
              })
            }
          />
          跳到步驟
        </label>
        <label className="flex items-center gap-1">
          <input
            type="radio"
            checked={exception.action.type === 'showMessage'}
            onChange={() =>
              setAction({
                type: 'showMessage',
                text: exception.action.type === 'showMessage' ? exception.action.text : '',
              })
            }
          />
          顯示訊息
        </label>
      </div>

      {exception.action.type === 'goToStep' ? (
        <select
          className="block w-full rounded border border-slate-300 bg-white px-2 py-1 text-sm"
          value={exception.action.targetId ?? ''}
          onChange={(e) => setAction({ type: 'goToStep', targetId: e.target.value || null })}
        >
          <option value="">-- 選擇目標步驟 --</option>
          {steps
            .filter((s) => s._id !== selfId)
            .map((s, idx) => (
              <option key={s._id} value={s._id}>
                {(idx + 1).toString().padStart(2, '0')}. {s.name || '(無名稱)'}
              </option>
            ))}
        </select>
      ) : (
        <textarea
          className="block w-full rounded border border-slate-300 bg-white px-2 py-1 text-sm"
          rows={2}
          value={exception.action.text}
          onChange={(e) => setAction({ type: 'showMessage', text: e.target.value })}
          placeholder="顯示給使用者看的訊息"
        />
      )}
    </div>
  );
}
