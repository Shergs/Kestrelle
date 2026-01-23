import { useState } from "react";
import { Outlet } from "react-router-dom";
import { Sidebar } from "./Sidebar";

export function AppShell() {
  const [isNavOpen, setIsNavOpen] = useState(false);

  return (
    <div className="min-h-screen bg-slate-950 text-slate-100">
      <div className="mx-auto max-w-[1700px] px-6 pt-6 pb-10">
        <div className="mb-4 flex items-center justify-between lg:hidden">
          <button
            type="button"
            onClick={() => setIsNavOpen(true)}
            className="grid h-11 w-11 place-items-center rounded-2xl border border-slate-800 bg-slate-900/60 text-slate-200 shadow-sm"
            aria-label="Open navigation"
          >
            <svg viewBox="0 0 24 24" className="h-5 w-5 fill-current">
              <path d="M4 6h16v2H4zM4 11h16v2H4zM4 16h16v2H4z" />
            </svg>
          </button>
          <div className="flex items-center gap-3">
            <img
              src="/Kestrelle.png"
              alt="Kestrelle"
              className="h-9 w-9 rounded-xl object-cover ring-1 ring-slate-800"
            />
            <div className="text-base font-semibold">Kestrelle</div>
          </div>
        </div>

        {isNavOpen && (
          <button
            type="button"
            className="fixed inset-0 z-40 bg-black/60 backdrop-blur-sm lg:hidden"
            onClick={() => setIsNavOpen(false)}
            aria-label="Close navigation"
          />
        )}

        <div
          className={[
            "fixed inset-y-0 left-0 z-50 w-72 p-4 transition-transform duration-300 ease-out lg:hidden",
            isNavOpen ? "translate-x-0" : "-translate-x-full",
          ].join(" ")}
        >
          <div className="h-full">
            <Sidebar />
          </div>
        </div>

        <div className="grid grid-cols-1 gap-4 lg:grid-cols-[260px_1fr]">
          <div className="hidden lg:block">
            <Sidebar />
          </div>
          <main className="rounded-2xl border border-slate-800 bg-slate-900/40 p-4 shadow-[0_0_0_1px_rgba(15,23,42,0.4)] lg:p-6">
            <Outlet />
          </main>
        </div>
      </div>
    </div>
  );
}
