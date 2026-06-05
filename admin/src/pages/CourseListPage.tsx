import { useEffect, useState } from 'react';

import { api } from '../api';
import type { CourseSummary } from '../types';

interface Props {
  company: string;
  onOpen: (name: string) => void;
}

export function CourseListPage({ company, onOpen }: Props) {
  const [courses, setCourses] = useState<CourseSummary[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const [creating, setCreating] = useState(false);
  const [newName, setNewName] = useState('');
  const [newDisplay, setNewDisplay] = useState('');

  const refresh = () => {
    setLoading(true);
    setError(null);
    api
      .listCourses(company)
      .then((r) => setCourses(r.courses))
      .catch((e) => setError(String(e)))
      .finally(() => setLoading(false));
  };

  useEffect(refresh, [company]);

  async function handleCreate() {
    if (!newName.trim() || !newDisplay.trim()) return;
    try {
      await api.createCourse(company, { name: newName.trim(), displayName: newDisplay.trim() });
      setCreating(false);
      setNewName('');
      setNewDisplay('');
      refresh();
    } catch (e) {
      alert(`新增失敗：${e}`);
    }
  }

  async function handleDelete(name: string) {
    if (!confirm(`確定要刪除「${name}」？檔案會搬到 _trash 仍可回復。`)) return;
    try {
      await api.deleteCourse(company, name);
      refresh();
    } catch (e) {
      alert(`刪除失敗：${e}`);
    }
  }

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="text-xl font-semibold">
          {company} 的課程 <span className="text-sm font-normal text-slate-500">({courses.length})</span>
        </h1>
        <button
          onClick={() => setCreating((v) => !v)}
          className="rounded bg-slate-900 px-3 py-1.5 text-sm text-white hover:bg-slate-700"
        >
          {creating ? '取消' : '+ 新增課程'}
        </button>
      </div>

      {creating && (
        <div className="rounded border border-slate-200 bg-white p-4 space-y-3">
          <div className="grid grid-cols-2 gap-3">
            <label className="block">
              <span className="text-xs font-medium text-slate-600">課程 ID（slug）</span>
              <input
                value={newName}
                onChange={(e) => setNewName(e.target.value)}
                placeholder="例如 my-new-course"
                pattern="[a-z0-9][a-z0-9_-]{0,62}"
                className="mt-1 block w-full rounded border border-slate-300 px-2 py-1.5 text-sm"
              />
              <span className="text-xs text-slate-400">
                小寫字母、數字、底線、連字號；用於檔案系統與 URL
              </span>
            </label>
            <label className="block">
              <span className="text-xs font-medium text-slate-600">顯示名稱</span>
              <input
                value={newDisplay}
                onChange={(e) => setNewDisplay(e.target.value)}
                placeholder="例如 我的新課程"
                className="mt-1 block w-full rounded border border-slate-300 px-2 py-1.5 text-sm"
              />
            </label>
          </div>
          <button
            onClick={handleCreate}
            disabled={!newName.trim() || !newDisplay.trim()}
            className="rounded bg-emerald-600 px-3 py-1.5 text-sm text-white hover:bg-emerald-500 disabled:opacity-40"
          >
            建立
          </button>
        </div>
      )}

      {loading && <p className="text-sm text-slate-500">載入中…</p>}
      {error && <p className="text-sm text-rose-600">錯誤：{error}</p>}

      <div className="overflow-hidden rounded border border-slate-200 bg-white">
        <table className="w-full text-sm">
          <thead className="bg-slate-50 text-slate-600">
            <tr>
              <th className="px-4 py-2 text-left font-medium">ID</th>
              <th className="px-4 py-2 text-left font-medium">顯示名稱</th>
              <th className="px-4 py-2 text-right font-medium">動作</th>
            </tr>
          </thead>
          <tbody>
            {courses.map((c) => (
              <tr key={c.name} className="border-t border-slate-100 hover:bg-slate-50">
                <td className="px-4 py-2 font-mono text-xs text-slate-700">{c.name}</td>
                <td className="px-4 py-2">{c.displayName}</td>
                <td className="px-4 py-2 text-right space-x-2">
                  <button
                    onClick={() => onOpen(c.name)}
                    className="text-blue-600 hover:underline"
                  >
                    編輯
                  </button>
                  <button
                    onClick={() => handleDelete(c.name)}
                    className="text-rose-600 hover:underline"
                  >
                    刪除
                  </button>
                </td>
              </tr>
            ))}
            {!loading && courses.length === 0 && (
              <tr>
                <td colSpan={3} className="px-4 py-8 text-center text-slate-400">
                  尚無課程
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>
    </div>
  );
}
