import { useEffect, useRef, useState } from 'react';

import { api } from '../api';

type Kind = 'image' | 'video';

interface Props {
  company: string;
  course: string;
  // Filenames currently referenced by steps — used to badge "in use" and to
  // warn before delete.
  referencedFiles: Set<string>;
  // Lifted refresh trigger so callers can re-pull lists after upload.
  onChange?: () => void;
  // Backend auto-clears CSV refs on delete; the page needs to reload the
  // course so the step editor sees the cleared media field.
  onReferencesCleared?: () => void;
}

export function MediaLibrary({
  company,
  course,
  referencedFiles,
  onChange,
  onReferencesCleared,
}: Props) {
  const [images, setImages] = useState<string[]>([]);
  const [videos, setVideos] = useState<string[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  function refresh() {
    setLoading(true);
    setError(null);
    Promise.all([api.listFiles(company, course, 'image'), api.listFiles(company, course, 'video')])
      .then(([img, vid]) => {
        setImages(img.files);
        setVideos(vid.files);
      })
      .catch((e) => setError(String(e)))
      .finally(() => setLoading(false));
  }

  useEffect(refresh, [company, course]);

  async function handleDelete(kind: Kind, filename: string) {
    if (referencedFiles.has(filename)) {
      if (
        !confirm(
          `「${filename}」目前被某步驟引用。刪除後：\n` +
            `  • 檔案搬到 _trash/，可手動復原\n` +
            `  • 引用它的步驟，媒體欄會自動清空（並備份 CSV）\n\n` +
            `確定要刪除？`,
        )
      )
        return;
    } else {
      if (!confirm(`確定要刪除 ${filename}？檔案會搬到 _trash/，仍可手動復原。`)) return;
    }
    try {
      const result = await api.deleteFile(company, course, kind, filename);
      refresh();
      onChange?.();
      if (result.cleared_refs > 0) {
        onReferencesCleared?.();
      }
    } catch (e) {
      alert(`刪除失敗：${e}`);
    }
  }

  return (
    <div className="space-y-4">
      {error && <p className="text-sm text-rose-600">錯誤：{error}</p>}

      <div className="grid grid-cols-2 gap-4">
        <Bucket
          title="圖片"
          kind="image"
          files={images}
          loading={loading}
          company={company}
          course={course}
          referencedFiles={referencedFiles}
          onUploaded={() => {
            refresh();
            onChange?.();
          }}
          onDelete={(name) => handleDelete('image', name)}
        />
        <Bucket
          title="影片"
          kind="video"
          files={videos}
          loading={loading}
          company={company}
          course={course}
          referencedFiles={referencedFiles}
          onUploaded={() => {
            refresh();
            onChange?.();
          }}
          onDelete={(name) => handleDelete('video', name)}
        />
      </div>
    </div>
  );
}

interface BucketProps {
  title: string;
  kind: Kind;
  files: string[];
  loading: boolean;
  company: string;
  course: string;
  referencedFiles: Set<string>;
  onUploaded: () => void;
  onDelete: (filename: string) => void;
}

function Bucket({
  title,
  kind,
  files,
  loading,
  company,
  course,
  referencedFiles,
  onUploaded,
  onDelete,
}: BucketProps) {
  const inputRef = useRef<HTMLInputElement>(null);
  const [uploading, setUploading] = useState(false);

  async function handleUpload(fl: FileList | null) {
    if (!fl || fl.length === 0) return;
    setUploading(true);
    try {
      for (const f of Array.from(fl)) {
        await api.uploadFile(company, course, kind, f);
      }
      onUploaded();
    } catch (e) {
      alert(`上傳失敗：${e}`);
    } finally {
      setUploading(false);
      if (inputRef.current) inputRef.current.value = '';
    }
  }

  const accept = kind === 'image' ? 'image/*' : 'video/*';

  return (
    <div className="rounded border border-slate-200 bg-slate-50 p-3 space-y-2">
      <div className="flex items-center justify-between">
        <h3 className="text-xs font-semibold uppercase tracking-wide text-slate-500">
          {title} ({files.length})
        </h3>
        <button
          onClick={() => inputRef.current?.click()}
          disabled={uploading}
          className="text-xs text-emerald-600 hover:text-emerald-700 disabled:opacity-40"
        >
          {uploading ? '上傳中…' : '+ 上傳'}
        </button>
        <input
          ref={inputRef}
          type="file"
          accept={accept}
          multiple
          className="hidden"
          onChange={(e) => handleUpload(e.target.files)}
        />
      </div>

      {loading && <p className="text-xs text-slate-400">載入中…</p>}
      {!loading && files.length === 0 && <p className="text-xs text-slate-400">尚未上傳任何{title}</p>}

      {files.length > 0 && kind === 'image' && (
        <ul className="grid grid-cols-3 gap-2">
          {files.map((f) => (
            <li key={f} className="group relative">
              <div className="aspect-square overflow-hidden rounded border border-slate-200 bg-white">
                <img
                  src={api.fileUrl(company, course, 'image', f)}
                  alt={f}
                  className="h-full w-full object-cover"
                />
              </div>
              <div className="mt-1 flex items-center gap-1">
                <span className="flex-1 truncate font-mono text-[10px] text-slate-600" title={f}>
                  {f}
                </span>
                {referencedFiles.has(f) && (
                  <span className="rounded bg-blue-100 px-1 text-[9px] text-blue-700">使用中</span>
                )}
                <button
                  onClick={() => onDelete(f)}
                  className="text-[10px] text-rose-600 hover:text-rose-700"
                >
                  ×
                </button>
              </div>
            </li>
          ))}
        </ul>
      )}

      {files.length > 0 && kind === 'video' && (
        <ul className="space-y-1">
          {files.map((f) => (
            <li key={f} className="flex items-center gap-2 text-sm">
              <span className="flex-1 truncate font-mono text-xs" title={f}>
                {f}
              </span>
              {referencedFiles.has(f) && (
                <span className="rounded bg-blue-100 px-1.5 py-0.5 text-[10px] text-blue-700">
                  使用中
                </span>
              )}
              <a
                href={api.fileUrl(company, course, 'video', f)}
                target="_blank"
                rel="noreferrer"
                className="text-xs text-blue-600 hover:underline"
              >
                預覽
              </a>
              <button
                onClick={() => onDelete(f)}
                className="text-xs text-rose-600 hover:text-rose-700"
              >
                刪除
              </button>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
