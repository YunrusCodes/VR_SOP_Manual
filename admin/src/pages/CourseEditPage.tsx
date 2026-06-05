import { useEffect, useMemo, useState } from 'react';

import { api } from '../api';
import { MediaLibrary } from '../components/MediaLibrary';
import { StepList } from '../components/StepList';
import { fromDraft, toDraft } from '../courseDraft';
import type { CourseDraft, StepDraft } from '../types';

interface Props {
  company: string;
  courseName: string;
  onBack: () => void;
}

export function CourseEditPage({ company, courseName, onBack }: Props) {
  const [draft, setDraft] = useState<CourseDraft | null>(null);
  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [savedAt, setSavedAt] = useState<string | null>(null);
  const [dirty, setDirty] = useState(false);
  // Ticks when the backend rewrote the CSV behind our back (e.g. after a media
  // delete cleared references). Used to refetch the draft.
  const [courseRev, setCourseRev] = useState(0);

  // File lists cached at the page level so the StepEditor datalist and the
  // MediaLibrary stay in sync. `mediaRev` ticks on upload/delete to force the
  // MediaLibrary to refetch and to recompute step thumbnails.
  const [images, setImages] = useState<string[]>([]);
  const [videos, setVideos] = useState<string[]>([]);
  const [mediaRev, setMediaRev] = useState(0);

  useEffect(() => {
    setLoading(true);
    setError(null);
    api
      .getCourse(company, courseName)
      .then((c) => {
        setDraft(toDraft(c));
        setDirty(false);
      })
      .catch((e) => setError(String(e)))
      .finally(() => setLoading(false));
  }, [company, courseName, courseRev]);

  useEffect(() => {
    Promise.all([
      api.listFiles(company, courseName, 'image').catch(() => ({ files: [] as string[] })),
      api.listFiles(company, courseName, 'video').catch(() => ({ files: [] as string[] })),
    ]).then(([i, v]) => {
      setImages(i.files);
      setVideos(v.files);
    });
  }, [company, courseName, mediaRev]);

  function update<K extends keyof CourseDraft>(key: K, value: CourseDraft[K]) {
    if (!draft) return;
    setDraft({ ...draft, [key]: value });
    setDirty(true);
  }

  function setSteps(next: StepDraft[]) {
    if (!draft) return;
    setDraft({ ...draft, steps: next });
    setDirty(true);
  }

  async function handleSave() {
    if (!draft) return;
    setSaving(true);
    setError(null);
    try {
      const wire = fromDraft(draft);
      await api.putCourse(company, wire);
      const fresh = await api.getCourse(company, courseName);
      setDraft(toDraft(fresh));
      setDirty(false);
      setSavedAt(new Date().toLocaleTimeString());
    } catch (e) {
      setError(String(e));
    } finally {
      setSaving(false);
    }
  }

  const referencedFiles = useMemo(() => {
    const set = new Set<string>();
    draft?.steps.forEach((s) => {
      if (s.media.filename) set.add(s.media.filename);
    });
    return set;
  }, [draft]);

  if (loading) return <p className="text-sm text-slate-500">載入中…</p>;
  if (error && !draft) return <p className="text-sm text-rose-600">錯誤：{error}</p>;
  if (!draft) return null;

  return (
    <div className="space-y-6">
      <div className="flex items-center gap-3">
        <button onClick={onBack} className="text-sm text-slate-500 hover:text-slate-700">
          ← 返回列表
        </button>
        <h1 className="text-xl font-semibold">{draft.displayName}</h1>
        <span className="text-xs text-slate-400 font-mono">{draft.name}</span>
        <div className="ml-auto flex items-center gap-3">
          {dirty && <span className="text-xs text-amber-600">有未儲存的變更</span>}
          {savedAt && !dirty && <span className="text-xs text-emerald-600">已儲存 {savedAt}</span>}
          <button
            onClick={handleSave}
            disabled={saving || !dirty}
            className="rounded bg-slate-900 px-3 py-1.5 text-sm text-white hover:bg-slate-700 disabled:opacity-40"
          >
            {saving ? '儲存中…' : '儲存'}
          </button>
        </div>
      </div>

      {error && <p className="text-sm text-rose-600">錯誤：{error}</p>}

      <section className="rounded border border-slate-200 bg-white p-4 space-y-3">
        <h2 className="text-sm font-semibold text-slate-700">基本資料</h2>
        <label className="block">
          <span className="text-xs font-medium text-slate-600">顯示名稱</span>
          <input
            value={draft.displayName}
            onChange={(e) => update('displayName', e.target.value)}
            className="mt-1 block w-full rounded border border-slate-300 px-2 py-1.5 text-sm"
          />
        </label>
        <label className="block">
          <span className="text-xs font-medium text-slate-600">課程簡介（CSV 第一列）</span>
          <textarea
            value={draft.introduction}
            onChange={(e) => update('introduction', e.target.value)}
            rows={5}
            className="mt-1 block w-full rounded border border-slate-300 px-2 py-1.5 text-sm font-sans"
          />
        </label>
      </section>

      <section className="rounded border border-slate-200 bg-white p-4 space-y-3">
        <h2 className="text-sm font-semibold text-slate-700">媒體庫</h2>
        <p className="text-xs text-slate-400">
          上傳到 storage 後，步驟編輯區的「媒體檔名」欄會跳出對應的下拉提示。
        </p>
        <MediaLibrary
          company={company}
          course={courseName}
          referencedFiles={referencedFiles}
          onChange={() => setMediaRev((v) => v + 1)}
          onReferencesCleared={() => {
            if (dirty) {
              alert(
                '檔案已刪除，後端已自動清掉引用它的步驟。但你目前有未儲存的變更，所以畫面不會自動重新載入——存檔或捨棄變更後再次進入這頁即可看到最新狀態。',
              );
            } else {
              setCourseRev((v) => v + 1);
            }
          }}
        />
      </section>

      <section className="rounded border border-slate-200 bg-white p-4 space-y-3">
        <h2 className="text-sm font-semibold text-slate-700">
          步驟 <span className="text-slate-400">({draft.steps.length})</span>
        </h2>
        <p className="text-xs text-slate-400">
          拖曳 <span className="font-mono">⋮⋮</span> 排序、+ 上面 插入、刪除 移除。
        </p>
        <StepList
          steps={draft.steps}
          availableImages={images}
          availableVideos={videos}
          imageUrl={(filename) => api.fileUrl(company, courseName, 'image', filename)}
          onChange={setSteps}
        />
      </section>
    </div>
  );
}
