import React, {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useState,
} from "react";

type AuthUser = {
  id: string;
  username: string;
  avatarUrl?: string;
};

type AuthState = {
  isAuthenticated: boolean;
  user: AuthUser | null;
  isLoading: boolean;
};

type AuthContextValue = AuthState & {
  beginDiscordLogin: (returnUrl?: string) => void;
  signOut: () => Promise<void>;
  refresh: () => Promise<void>;

  // Optional: keep for local UI testing
  devSignIn: (username?: string) => void;
};

const AuthContext = createContext<AuthContextValue | null>(null);

/**
 * Calls the API to determine the *real* authentication state (cookie-based).
 * This is the source of truth, not localStorage.
 */
async function fetchMe(): Promise<{ authenticated: boolean; discordUserId?: string; username?: string; avatarUrl?: string }> {
  const res = await fetch("/api/auth/me", {
    method: "GET",
    credentials: "include",
    headers: { Accept: "application/json" },
  });

  if (!res.ok) {
    return { authenticated: false };
  }

  return (await res.json()) ?? { authenticated: false };
}

export function AuthProvider({ children }: { children: React.ReactNode }) {
  const [state, setState] = useState<AuthState>({
    isAuthenticated: false,
    user: null,
    isLoading: true,
  });

  const refresh = useCallback(async () => {
    setState((s) => ({ ...s, isLoading: true }));

    const me = await fetchMe();

    if (me.authenticated) {
      setState({
        isAuthenticated: true,
        isLoading: false,
        user: {
          id: me.discordUserId ?? "discord",
          username: me.username ?? "User",
          avatarUrl: me.avatarUrl,
        },
      });
    } else {
      setState({
        isAuthenticated: false,
        isLoading: false,
        user: null,
      });
    }
  }, []);

  // Critical: run on initial load (including after Discord redirects back)
  useEffect(() => {
    void refresh();
  }, [refresh]);

  const value = useMemo<AuthContextValue>(() => {
    return {
      ...state,
      refresh,

      beginDiscordLogin: (returnUrl?: string) => {
        const r = (returnUrl ?? "/dashboard").trim() || "/dashboard";
        window.location.href = `/api/auth/discord/login?returnUrl=${encodeURIComponent(r)}`;
      },

      signOut: async () => {
        // If your API supports logout, use it; otherwise this is harmless.
        try {
          await fetch("/api/auth/logout", {
            method: "POST",
            credentials: "include",
          });
        } catch {
          // ignore
        }

        // Re-check server auth state and update UI
        await refresh();
      },

      devSignIn: (username?: string) => {
        const name = (username ?? "").trim() || "User";
        setState({
          isAuthenticated: true,
          isLoading: false,
          user: {
            id: crypto.randomUUID(),
            username: name,
          },
        });
      },
    };
  }, [state, refresh]);

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth() {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error("useAuth must be used within AuthProvider.");
  return ctx;
}
