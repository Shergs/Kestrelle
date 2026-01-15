import { useEffect, useState } from "react";

type StatusResponse = {
  service: string;
  version: string;
  utcNow: string;
};

export default function App() {
  const [status, setStatus] = useState<StatusResponse | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

  async function loadStatus() {
    setLoading(true);
    setError(null);

    try {
      const res = await fetch("/api/status", {
        headers: { Accept: "application/json" },
      });

      if (!res.ok) {
        const text = await res.text();
        throw new Error(`GET /api/status failed (${res.status}): ${text}`);
      }

      const json = (await res.json()) as StatusResponse;
      setStatus(json);
    } catch (e: any) {
      setError(e?.message ?? String(e));
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    loadStatus();
  }, []);

  return (
    <div className="min-h-screen bg-slate-950 text-slate-100">
      <div className="mx-auto max-w-3xl p-6">
        <h1 className="text-3xl font-semibold">Kestrelle Dashboard Client</h1>
        <p className="mt-2 text-slate-400">
          React frontend calling .NET API via Nginx reverse proxy
        </p>

        <div className="mt-6 rounded-2xl border border-slate-800 bg-slate-900/50 p-5">
          <div className="flex items-center justify-between">
            <div className="text-lg font-semibold">API Status</div>
            <button
              className="rounded-xl border border-slate-700 bg-slate-950 px-3 py-2 text-sm hover:bg-slate-900"
              onClick={loadStatus}
            >
              Refresh
            </button>
          </div>

          {loading && <div className="mt-3 text-slate-400">Loading…</div>}

          {!loading && error && (
            <div className="mt-3 rounded-xl border border-red-900 bg-red-950/40 p-3 text-red-200">
              {error}
            </div>
          )}

          {!loading && !error && status && (
            <div className="mt-3 space-y-1 text-sm">
              <div>
                <span className="text-slate-400">Service:</span>{" "}
                <span className="font-medium">{status.service}</span>
              </div>
              <div>
                <span className="text-slate-400">Version:</span>{" "}
                <span className="font-medium">{status.version}</span>
              </div>
              <div>
                <span className="text-slate-400">UTC:</span>{" "}
                <span className="font-medium">
                  {new Date(status.utcNow).toISOString()}
                </span>
              </div>
              <div className="pt-2 text-slate-400">
                Endpoint hit: <span className="font-mono">/api/status</span>
              </div>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
