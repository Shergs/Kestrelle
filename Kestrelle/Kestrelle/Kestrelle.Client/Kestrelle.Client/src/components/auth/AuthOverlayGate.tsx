import { ReactNode } from "react";
import { useAuth } from "../../state/auth/AuthContext";

export function AuthOverlayGate({
  children,
  title = "Authorization required",
  subtitle = "Authorize with Discord to unlock the dashboard.",
}: {
  children: ReactNode;
  title?: string;
  subtitle?: string;
}) {
  const auth = useAuth();

  if (auth.isAuthenticated) {
    return <>{children}</>;
  }

  return (
    <div className="relative">
      <div className="pointer-events-none select-none blur-sm opacity-60">{children}</div>

      <div className="absolute inset-0 grid place-items-center">
        <div className="w-full max-w-md rounded-2xl border border-slate-800 bg-slate-950/80 p-6 shadow-2xl backdrop-blur">
          <div className="text-lg font-semibold">{title}</div>
          <div className="mt-1 text-sm text-slate-400">{subtitle}</div>

          <div className="mt-5 flex flex-col gap-2">
            <button
              className="rounded-xl bg-indigo-500 px-4 py-2.5 text-sm font-semibold text-white hover:bg-indigo-400"
              onClick={() => auth.beginDiscordLogin()}
            >
              Authorize with Discord
            </button>

            {/* Temporary helper so you can validate UX while API OAuth is not done */}
            <button
              className="rounded-xl border border-slate-700 bg-slate-950 px-4 py-2.5 text-sm text-slate-200 hover:bg-slate-900"
              onClick={() => auth.devSignIn("Shaun")}
            >
              Dev sign-in (temporary)
            </button>

            <div className="mt-2 text-xs text-slate-500">
              OAuth endpoint expected: <span className="font-mono">/api/auth/discord/start</span>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
