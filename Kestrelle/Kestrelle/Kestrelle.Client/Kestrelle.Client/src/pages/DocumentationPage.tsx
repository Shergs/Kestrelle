export function DocumentationPage() {
  return (
    <div className="space-y-5">
      <div>
        <h2 className="text-xl font-semibold">Documentation</h2>
        <p className="mt-1 text-sm text-slate-400">
          Operator notes and developer docs for Kestrelle.
        </p>
      </div>

      <div className="rounded-2xl border border-slate-800 bg-slate-950/40 p-5">
        <div className="text-sm font-semibold">Local URLs</div>
        <div className="mt-2 space-y-2 text-sm text-slate-300">
          <div>
            UI: <span className="font-mono text-slate-200">http://localhost:8080</span>
          </div>
          <div>
            API Status (via proxy):{" "}
            <span className="font-mono text-slate-200">http://localhost:8080/api/status</span>
          </div>
        </div>
      </div>

      <div className="rounded-2xl border border-slate-800 bg-slate-950/40 p-5">
        <div className="text-sm font-semibold">Auth roadmap</div>
        <ol className="mt-2 list-decimal space-y-1 pl-5 text-sm text-slate-400">
          <li>Implement Discord OAuth start/callback in API.</li>
          <li>Set secure session cookie (or JWT) on success.</li>
          <li>Expose `/api/me` and `/api/guilds` for UI.</li>
          <li>Add SignalR for live queue/now-playing.</li>
        </ol>
      </div>
    </div>
  );
}
