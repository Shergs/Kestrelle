import { useLocation, useNavigate } from "react-router-dom";
import { useAuth } from "../state/auth/AuthContext";

export function LoginPage() {
  const auth = useAuth();
  const navigate = useNavigate();
  const location = useLocation() as any;

  const from = location?.state?.from ?? "/dashboard";

  return (
    <div className="min-h-screen bg-slate-950 text-slate-100">
      <div className="mx-auto flex min-h-screen max-w-6xl items-center px-6 py-10">
        <div className="grid w-full gap-8 lg:grid-cols-2">
          <div className="rounded-3xl border border-slate-800 bg-slate-900/40 p-8">
            <div className="inline-flex items-center gap-3">
              <div className="grid h-10 w-10 place-items-center rounded-2xl bg-indigo-500/15 text-indigo-200 ring-1 ring-indigo-500/30">
                K
              </div>
              <div>
                <div className="text-lg font-semibold">Sign in to Kestrelle</div>
                <div className="text-sm text-slate-400">Authorize with Discord to continue</div>
              </div>
            </div>

            <div className="mt-6 space-y-3">
              <button
                className="w-full rounded-2xl bg-indigo-500 px-4 py-3 text-sm font-semibold text-white hover:bg-indigo-400"
                onClick={() => auth.beginDiscordLogin()}
              >
                Authorize with Discord
              </button>

              {/* Temporary helper until OAuth is implemented */}
              <button
                className="w-full rounded-2xl border border-slate-700 bg-slate-950 px-4 py-3 text-sm font-semibold text-slate-200 hover:bg-slate-900"
                onClick={() => {
                  auth.devSignIn("Shaun");
                  navigate(from, { replace: true });
                }}
              >
                Dev sign-in (temporary)
              </button>

              <button
                className="w-full rounded-2xl border border-slate-800 bg-transparent px-4 py-3 text-sm text-slate-300 hover:bg-slate-900/30"
                onClick={() => navigate("/overview")}
              >
                Back to Overview
              </button>

              <div className="pt-2 text-xs text-slate-500">
                Expected API endpoint: <span className="font-mono">/api/auth/discord/start</span>
              </div>
            </div>
          </div>

          <div className="rounded-3xl border border-slate-800 bg-slate-900/30 p-8">
            <div className="text-xl font-semibold">What you’ll get</div>
            <ul className="mt-4 space-y-3 text-sm text-slate-300">
              <li className="rounded-2xl border border-slate-800 bg-slate-950/40 p-4">
                Live “Now Playing” and queue visibility
              </li>
              <li className="rounded-2xl border border-slate-800 bg-slate-950/40 p-4">
                Server selection and per-guild controls
              </li>
              <li className="rounded-2xl border border-slate-800 bg-slate-950/40 p-4">
                Secure authorization flow (Discord OAuth)
              </li>
            </ul>
          </div>
        </div>
      </div>
    </div>
  );
}
