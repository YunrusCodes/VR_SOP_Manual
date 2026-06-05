import type { Course, CourseSummary, MediaKind } from './types';

type Kind = Exclude<MediaKind, 'none'>;

function filesUrl(company: string, course: string, kind: Kind): string {
  return `/companies/${encodeURIComponent(company)}/courses/${encodeURIComponent(course)}/files/${kind}`;
}

async function json<T>(res: Response): Promise<T> {
  if (!res.ok) {
    let detail = '';
    try {
      const body = await res.json();
      detail = body?.error?.message ?? body?.error?.code ?? '';
    } catch {
      /* ignore */
    }
    throw new Error(`${res.status} ${res.statusText}${detail ? `: ${detail}` : ''}`);
  }
  return (await res.json()) as T;
}

export const api = {
  listCompanies(): Promise<{ companies: string[] }> {
    return fetch(`/companies`).then((r) => json<{ companies: string[] }>(r));
  },

  listCourses(company: string): Promise<{ company: string; courses: CourseSummary[] }> {
    return fetch(`/companies/${encodeURIComponent(company)}/courses`).then((r) =>
      json<{ company: string; courses: CourseSummary[] }>(r),
    );
  },

  getCourse(company: string, course: string): Promise<Course> {
    return fetch(
      `/companies/${encodeURIComponent(company)}/courses/${encodeURIComponent(course)}/structured`,
    ).then((r) => json<Course>(r));
  },

  putCourse(company: string, course: Course): Promise<{ ok: true }> {
    return fetch(
      `/companies/${encodeURIComponent(company)}/courses/${encodeURIComponent(course.name)}/structured`,
      {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(course),
      },
    ).then((r) => json<{ ok: true }>(r));
  },

  createCourse(
    company: string,
    payload: { name: string; displayName: string; introduction?: string },
  ): Promise<{ ok: true; name: string }> {
    return fetch(`/companies/${encodeURIComponent(company)}/courses`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload),
    }).then((r) => json<{ ok: true; name: string }>(r));
  },

  deleteCourse(company: string, course: string): Promise<{ ok: true }> {
    return fetch(
      `/companies/${encodeURIComponent(company)}/courses/${encodeURIComponent(course)}`,
      { method: 'DELETE' },
    ).then((r) => json<{ ok: true }>(r));
  },

  listFiles(company: string, course: string, kind: Kind): Promise<{ files: string[] }> {
    return fetch(filesUrl(company, course, kind)).then((r) => json<{ files: string[] }>(r));
  },

  uploadFile(
    company: string,
    course: string,
    kind: Kind,
    file: File,
  ): Promise<{ ok: true; filename: string; size: number }> {
    const fd = new FormData();
    fd.append('file', file, file.name);
    return fetch(filesUrl(company, course, kind), { method: 'POST', body: fd }).then((r) =>
      json<{ ok: true; filename: string; size: number }>(r),
    );
  },

  deleteFile(
    company: string,
    course: string,
    kind: Kind,
    filename: string,
  ): Promise<{ ok: true; trashed_to: string; cleared_refs: number }> {
    return fetch(`${filesUrl(company, course, kind)}/${encodeURIComponent(filename)}`, {
      method: 'DELETE',
    }).then((r) => json<{ ok: true; trashed_to: string; cleared_refs: number }>(r));
  },

  fileUrl(company: string, course: string, kind: Kind, filename: string): string {
    return `${filesUrl(company, course, kind)}/${encodeURIComponent(filename)}`;
  },
};
