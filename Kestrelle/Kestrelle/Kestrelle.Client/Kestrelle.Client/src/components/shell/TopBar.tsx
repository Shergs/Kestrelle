import { Link, useLocation, useNavigate } from "react-router-dom";
import { useAuth } from "../../state/auth/AuthContext";

export function TopBar() {
  const auth = useAuth();
  const navigate = useNavigate();
  const location = useLocation();

  return (
    <header className="flex items-center justify-between rounded-2xl border border-slate-800 bg-slate-900/40 px-4 py-3">
      <Link to="/overview" className="flex items-center gap-3">
        <div className="grid h-9 w-9 place-items-center rounded-xl bg-indigo-500/15 text-indigo-200 ring-1 ring-indigo-500/30">
          K
        </div>
        <div>
          <div className="text-sm font-semibold leading-4">Kestrelle</div>
          <div className="text-xs text-slate-400">Modern control plane</div>
        </div>
      </Link>

      <div className="flex items-center gap-2">
        {auth.isAuthenticated && auth.user ? (
          <>
            <div className="hidden text-right sm:block">
              <div className="text-sm font-medium leading-4">{auth.user.username}</div>
              <div className="text-xs text-slate-400">Signed in</div>
            </div>
            <button
              className="rounded-xl border border-slate-700 bg-slate-950 px-3 py-2 text-sm hover:bg-slate-900"
              onClick={() => auth.signOut()}
            >
              Sign out
            </button>
          </>
        ) : (
          <>
            <button
              className="rounded-xl border border-slate-700 bg-slate-950 px-3 py-2 text-sm hover:bg-slate-900"
              onClick={() => navigate("/login", { state: { from: location.pathname } })}
            >
              Sign in
            </button>
          </>
        )}
      </div>
    </header>
  );
}
