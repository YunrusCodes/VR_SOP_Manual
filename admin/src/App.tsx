import { useEffect, useState } from 'react';
import { NavLink, Navigate, Route, Routes, useNavigate, useParams } from 'react-router-dom';

import { api } from './api';
import { CourseListPage } from './pages/CourseListPage';
import { CourseEditPage } from './pages/CourseEditPage';

const COMPANY_KEY = 'admin.company';

function useCompany(): [string | null, (c: string) => void, string[]] {
  const [companies, setCompanies] = useState<string[]>([]);
  const [company, setCompanyState] = useState<string | null>(
    () => localStorage.getItem(COMPANY_KEY),
  );

  useEffect(() => {
    api
      .listCompanies()
      .then((r) => {
        setCompanies(r.companies);
        if (!company && r.companies.length > 0) {
          setCompanyState(r.companies[0]);
          localStorage.setItem(COMPANY_KEY, r.companies[0]);
        }
      })
      .catch((e) => console.error('listCompanies failed', e));
  }, [company]);

  const setCompany = (c: string) => {
    setCompanyState(c);
    localStorage.setItem(COMPANY_KEY, c);
  };

  return [company, setCompany, companies];
}

function Topbar({
  company,
  companies,
  setCompany,
}: {
  company: string | null;
  companies: string[];
  setCompany: (c: string) => void;
}) {
  return (
    <header className="border-b border-slate-200 bg-white">
      <div className="mx-auto flex max-w-5xl items-center gap-6 px-6 py-3">
        <NavLink to="/" className="text-lg font-semibold text-slate-900">
          Inspection Admin
        </NavLink>
        <nav className="flex items-center gap-3 text-sm text-slate-600">
          <NavLink to="/" end className={({ isActive }) => (isActive ? 'text-slate-900 font-medium' : '')}>
            課程
          </NavLink>
        </nav>
        <div className="ml-auto flex items-center gap-2">
          <label className="text-sm text-slate-500">公司</label>
          <select
            className="rounded border border-slate-300 bg-white px-2 py-1 text-sm"
            value={company ?? ''}
            onChange={(e) => setCompany(e.target.value)}
          >
            {companies.length === 0 && <option value="">(載入中)</option>}
            {companies.map((c) => (
              <option key={c} value={c}>
                {c}
              </option>
            ))}
          </select>
        </div>
      </div>
    </header>
  );
}

function CourseListRoute({ company }: { company: string }) {
  const navigate = useNavigate();
  return (
    <CourseListPage
      company={company}
      onOpen={(name) => navigate(`/courses/${encodeURIComponent(name)}`)}
    />
  );
}

function CourseEditRoute({ company }: { company: string }) {
  const params = useParams<{ name: string }>();
  const navigate = useNavigate();
  if (!params.name) return <Navigate to="/" replace />;
  return (
    <CourseEditPage
      company={company}
      courseName={decodeURIComponent(params.name)}
      onBack={() => navigate('/')}
    />
  );
}

export function App() {
  const [company, setCompany, companies] = useCompany();
  return (
    <div className="flex min-h-full flex-col">
      <Topbar company={company} companies={companies} setCompany={setCompany} />
      <main className="mx-auto w-full max-w-5xl flex-1 px-6 py-6">
        {!company ? (
          <p className="text-slate-500">沒有可選的公司，請在 storage/ 下建立資料夾。</p>
        ) : (
          <Routes>
            <Route path="/" element={<CourseListRoute company={company} />} />
            <Route path="/courses/:name" element={<CourseEditRoute company={company} />} />
            <Route path="*" element={<Navigate to="/" replace />} />
          </Routes>
        )}
      </main>
    </div>
  );
}
