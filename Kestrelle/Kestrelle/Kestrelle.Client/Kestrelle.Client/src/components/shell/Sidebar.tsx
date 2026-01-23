import { useState } from "react";
import { NavLink } from "react-router-dom";

const navItem =
  "flex items-center gap-2 rounded-xl px-3 py-2 text-sm text-slate-300 hover:bg-slate-900 hover:text-slate-100";
const navItemActive =
  "bg-slate-900 text-slate-100 border border-slate-800 shadow-[0_0_0_1px_rgba(15,23,42,0.35)]";

export function Sidebar() {
  const [isProOpen, setIsProOpen] = useState(false);

  return (
    <aside className="rounded-2xl border border-slate-800 bg-slate-900/40 p-3">
      <div className="px-3 py-3">
        <div className="flex items-center gap-3">
          <img
            src="/Kestrelle_Transparent.png"
            alt="Kestrelle"
            className="h-9 w-9 rounded-xl object-contain ring-1 ring-slate-800"
          />
          <div>
            <div className="text-base font-semibold">Kestrelle</div>
          </div>
        </div>
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
        Tip: Keep Kestrelle happy and you will be too.
      </div>

      <div className="mt-4">
        <button
          type="button"
          onClick={() => setIsProOpen(true)}
          className="group relative flex w-full items-center justify-between overflow-hidden rounded-2xl border border-amber-400/30 bg-gradient-to-r from-amber-500/20 via-yellow-500/20 to-amber-500/20 px-4 py-3 text-sm font-semibold text-amber-100 shadow-[0_0_0_1px_rgba(251,191,36,0.2)] transition hover:border-amber-300/50 hover:from-amber-500/30 hover:via-yellow-500/30 hover:to-amber-500/30"
        >
          <span className="flex items-center gap-2">
            <span className="grid h-8 w-8 place-items-center rounded-xl bg-amber-500/20 text-amber-200 ring-1 ring-amber-400/30">
              ✦
            </span>
            Upgrade to Pro
          </span>
          <span className="text-xs text-amber-200/80">Coming soon</span>
          <span className="pointer-events-none absolute inset-0 bg-[radial-gradient(circle_at_top_left,rgba(251,191,36,0.18),transparent_60%)] opacity-0 transition group-hover:opacity-100" />
        </button>
      </div>

      {isProOpen && (
        <div className="fixed inset-0 z-50">
          <button
            type="button"
            className="absolute inset-0 bg-black/60 backdrop-blur-sm"
            onClick={() => setIsProOpen(false)}
            aria-label="Close upgrade modal"
          />
          <div className="relative mx-auto mt-24 w-[92%] max-w-md rounded-3xl border border-slate-800 bg-slate-950 p-6 shadow-2xl">
            <div className="flex items-center justify-between">
              <div className="text-sm font-semibold text-amber-100">Upgrade to Pro</div>
              <button
                type="button"
                onClick={() => setIsProOpen(false)}
                className="rounded-full border border-slate-800 bg-slate-900 px-2 py-1 text-xs text-slate-300 hover:bg-slate-800"
              >
                Close
              </button>
            </div>
            <div className="mt-3 rounded-2xl border border-amber-400/20 bg-amber-500/10 p-4 text-sm text-amber-100">
              Coming soon... Sign up for updates
            </div>
            <div className="mt-4">
              <label className="text-xs text-slate-400">Email</label>
              <input
                type="email"
                placeholder="you@example.com"
                className="mt-2 w-full rounded-2xl border border-slate-800 bg-slate-950/60 px-4 py-2 text-sm text-slate-100 outline-none focus:border-amber-400/40 focus:ring-2 focus:ring-amber-400/20"
              />
              <button
                type="button"
                className="mt-3 w-full rounded-2xl bg-gradient-to-r from-amber-500 to-yellow-500 px-4 py-2 text-sm font-semibold text-slate-900 shadow-lg shadow-amber-500/30 transition hover:from-amber-400 hover:to-yellow-400"
              >
                Notify me
              </button>
            </div>
          </div>
        </div>
      )}
    </aside>
  );
}
