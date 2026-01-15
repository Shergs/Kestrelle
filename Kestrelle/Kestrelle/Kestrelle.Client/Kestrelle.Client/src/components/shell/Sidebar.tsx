import { NavLink } from "react-router-dom";

const navItem =
  "flex items-center gap-2 rounded-xl px-3 py-2 text-sm text-slate-300 hover:bg-slate-900 hover:text-slate-100";
const navItemActive =
  "bg-slate-900 text-slate-100 border border-slate-800 shadow-[0_0_0_1px_rgba(15,23,42,0.35)]";

export function Sidebar() {
  return (
    <aside className="rounded-2xl border border-slate-800 bg-slate-900/40 p-3">
      <div className="px-3 py-3">
        <div className="text-base font-semibold">Kestrelle</div>
        <div className="mt-1 text-xs text-slate-400">Discord bot dashboard</div>
      </div>

      <nav className="mt-2 space-y-1">
        <NavLink
          to="/overview"
          className={({ isActive }) => `${navItem} ${isActive ? navItemActive : ""}`}
        >
          Overview
        </NavLink>

        <NavLink
          to="/dashboard"
          className={({ isActive }) => `${navItem} ${isActive ? navItemActive : ""}`}
        >
          Dashboard
        </NavLink>

        <NavLink
          to="/documentation"
          className={({ isActive }) => `${navItem} ${isActive ? navItemActive : ""}`}
        >
          Documentation
        </NavLink>
      </nav>

      <div className="mt-4 rounded-xl border border-slate-800 bg-slate-950/50 p-3 text-xs text-slate-400">
        Tip: keep API calls relative (e.g. <span className="font-mono">/api/status</span>) so
        Nginx can proxy cleanly.
      </div>
    </aside>
  );
}
