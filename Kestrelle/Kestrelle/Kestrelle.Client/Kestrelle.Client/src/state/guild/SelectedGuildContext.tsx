import React, { createContext, useContext, useEffect, useMemo, useState, useCallback } from "react";
import { useAuth } from "../auth/AuthContext";

export type GuildOption = {
  id: string;
  name: string;
  iconUrl?: string | null;
  isDev?: boolean;
};

type GuildContextValue = {
  guilds: GuildOption[];
  selectedGuildId: string | null;
  selectGuild: (guildId: string | null) => void;
  isLoading: boolean;
  error: string | null;
  refreshGuilds: () => Promise<void>;
};

const GuildContext = createContext<GuildContextValue | null>(null);

const STORAGE_KEY = "kestrelle.selectedGuildId.v1";

// Keep your dev option
const DEV_GUILD: GuildOption = {
  id: "783190942806835201",
  name: "Kestrelle Dev Server",
  isDev: true,
};

function loadSelected(): string | null {
  try {
    return localStorage.getItem(STORAGE_KEY);
  } catch {
    return null;
  }
}

function saveSelected(id: string | null) {
  try {
    if (!id) localStorage.removeItem(STORAGE_KEY);
    else localStorage.setItem(STORAGE_KEY, id);
  } catch {
    // ignore
  }
}

async function fetchGuildsFromApi(): Promise<GuildOption[]> {
  const res = await fetch("/api/discord/available-guilds", {
    method: "GET",
    credentials: "include",
    headers: { Accept: "application/json" },
  });

  if (res.status === 401) return [];
  if (!res.ok) {
    const body = await res.text().catch(() => "");
    throw new Error(`Failed to load guilds (${res.status}): ${body || res.statusText}`);
  }

  const data = (await res.json()) as GuildOption[];
  return Array.isArray(data) ? data : [];
}

function mergeGuilds(devGuild: GuildOption | null, apiGuilds: GuildOption[]): GuildOption[] {
  const map = new Map<string, GuildOption>();

  // Add dev first so API can override if same id exists
  if (devGuild) {
    map.set(devGuild.id, devGuild);
  }

  for (const g of apiGuilds) {
    if (!g?.id) continue;
    map.set(g.id, { ...g, isDev: map.get(g.id)?.isDev ?? false });
  }

  // Sort: dev first, then alphabetical
  return Array.from(map.values()).sort((a, b) => {
    const aDev = a.isDev ? 0 : 1;
    const bDev = b.isDev ? 0 : 1;
    if (aDev !== bDev) return aDev - bDev;
    return a.name.localeCompare(b.name, undefined, { sensitivity: "base" });
  });
}

export function SelectedGuildProvider({ children }: { children: React.ReactNode }) {
  const { isAuthenticated } = useAuth();

  const [apiGuilds, setApiGuilds] = useState<GuildOption[]>([]);
  const [selectedGuildId, setSelectedGuildId] = useState<string | null>(() => loadSelected());
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // For now, always include. If you only want it in dev:
  // const includeDev = import.meta.env.DEV;
  const includeDev = true;

  const selectGuild = useCallback((guildId: string | null) => {
    setSelectedGuildId(guildId);
    saveSelected(guildId);
  }, []);

  const refreshGuilds = useCallback(async () => {
    setIsLoading(true);
    setError(null);

    try {
      const list = await fetchGuildsFromApi();
      setApiGuilds(list);

      const merged = mergeGuilds(includeDev ? DEV_GUILD : null, list);
      if (selectedGuildId && !merged.some((g) => g.id === selectedGuildId)) {
        selectGuild(null);
      }
    } catch (e) {
      const msg = e instanceof Error ? e.message : "Failed to load guilds.";
      setError(msg);
      setApiGuilds([]);
      // keep selection; UI gating will handle blur if required
    } finally {
      setIsLoading(false);
    }
  }, [includeDev, selectedGuildId, selectGuild]);

  useEffect(() => {
    if (!isAuthenticated) {
      setApiGuilds([]);
      return;
    }

    void refreshGuilds();
  }, [isAuthenticated, refreshGuilds]);

  const guilds = useMemo(() => {
    return mergeGuilds(includeDev ? DEV_GUILD : null, apiGuilds);
  }, [includeDev, apiGuilds]);

  const value = useMemo<GuildContextValue>(
    () => ({
      guilds,
      selectedGuildId,
      selectGuild,
      isLoading,
      error,
      refreshGuilds,
    }),
    [guilds, selectedGuildId, selectGuild, isLoading, error, refreshGuilds]
  );

  return <GuildContext.Provider value={value}>{children}</GuildContext.Provider>;
}

export function useSelectedGuild() {
  const ctx = useContext(GuildContext);
  if (!ctx) throw new Error("useSelectedGuild must be used within SelectedGuildProvider.");
  return ctx;
}
