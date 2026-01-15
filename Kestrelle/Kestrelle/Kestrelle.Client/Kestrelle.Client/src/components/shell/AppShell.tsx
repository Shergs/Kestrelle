import { Outlet } from "react-router-dom";
import { Sidebar } from "./Sidebar";
import { TopBar } from "./TopBar";

export function AppShell() {
  return (
    <div className="min-h-screen bg-slate-950 text-slate-100">
      <div className="mx-auto max-w-7xl px-4 py-4">
        <TopBar />
      </div>

      <div className="mx-auto max-w-7xl px-4 pb-10">
        <div className="grid grid-cols-1 gap-4 lg:grid-cols-[260px_1fr]">
          <Sidebar />
          <main className="rounded-2xl border border-slate-800 bg-slate-900/40 p-4 shadow-[0_0_0_1px_rgba(15,23,42,0.4)] lg:p-6">
            <Outlet />
          </main>
        </div>
      </div>
    </div>
  );
}
