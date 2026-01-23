import React, { createContext, useContext, useEffect, useMemo, useRef, useState } from "react";
import { HubConnection, HubConnectionBuilder, HubConnectionState, LogLevel } from "@microsoft/signalr";
import { useAuth } from "../auth/AuthContext";
import { useSelectedGuild } from "../guild/SelectedGuildContext";
import { useToast } from "../../components/toast/ToastContext";

type TrackDto = {
  title: string;
  author?: string | null;
  uri?: string | null;
  durationMs: number;
  artworkUrl?: string | null;
  requestedBy?: string | null;
};

export type NowPlayingPayload = {
  guildId: string;
  track: TrackDto | null;
  positionMs: number;
  isPaused: boolean;
  volume: number;
  updatedUtc: string;
};

export type QueueItemDto = {
  title: string;
  author?: string | null;
  uri?: string | null;
  durationMs: number;
  artworkUrl?: string | null;
  requestedBy?: string | null;
};

export type QueuePayload = {
  guildId: string;
  tracks: QueueItemDto[];
  updatedUtc: string;
};

type ToastPayload = {
  guildId: string;
  kind: "success" | "info" | "warning" | "error";
  message: string;
  user?: string | null;
  occurredUtc: string;
};

type RealtimeValue = {
  isConnected: boolean;
  nowPlayingByGuild: Record<string, NowPlayingPayload | undefined>;
  queueByGuild: Record<string, QueuePayload | undefined>;
};

const MusicRealtimeContext = createContext<RealtimeValue | null>(null);

export function MusicRealtimeProvider({ children }: { children: React.ReactNode }) {
  const { isAuthenticated } = useAuth();
  const { guilds, selectedGuildId } = useSelectedGuild();
  const { push } = useToast();

  const [nowPlayingByGuild, setNowPlayingByGuild] = useState<Record<string, NowPlayingPayload | undefined>>({});
  const [queueByGuild, setQueueByGuild] = useState<Record<string, QueuePayload | undefined>>({});
  const [isConnected, setIsConnected] = useState(false);

  const connRef = useRef<HubConnection | null>(null);
  const selectedGuildRef = useRef<string | null>(null);

  useEffect(() => {
    selectedGuildRef.current = selectedGuildId ?? null;
  }, [selectedGuildId]);

  // Start/stop connection based on auth
  useEffect(() => {
    if (!isAuthenticated) {
      setIsConnected(false);
      connRef.current?.stop().catch(() => {});
      connRef.current = null;
      return;
    }

    const conn = new HubConnectionBuilder()
      .withUrl("/hubs/music", { withCredentials: true })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Information)
      .build();

    conn.on("NowPlayingUpdated", (p: NowPlayingPayload) => {
      setNowPlayingByGuild((prev) => {
        const existing = prev[p.guildId];
        if (existing?.track && p.track) {
          const sameTrack =
            (p.track.uri && existing.track.uri && p.track.uri === existing.track.uri) ||
            p.track.title === existing.track.title;

          if (sameTrack && p.positionMs <= 0 && existing.positionMs > 0) {
            return {
              ...prev,
              [p.guildId]: {
                ...p,
                positionMs: existing.positionMs,
                updatedUtc: existing.updatedUtc,
              },
            };
          }
        }

        return { ...prev, [p.guildId]: p };
      });
    });

    conn.on("QueueUpdated", (p: QueuePayload) => {
      setQueueByGuild((prev) => ({ ...prev, [p.guildId]: p }));
    });

    conn.on("Toast", (p: ToastPayload) => {
      const activeGuild = selectedGuildRef.current;
      if (activeGuild && p.guildId !== activeGuild) return;
      const guildName = guilds.find((g) => g.id === p.guildId)?.name;
      push({
        kind: p.kind,
        title: guildName ? `${guildName}` : "Bot event",
        message: p.user ? `${p.user}: ${p.message}` : p.message,
      });
    });

    conn.onreconnected(() => setIsConnected(true));
    conn.onclose(() => setIsConnected(false));

    conn.start()
      .then(() => setIsConnected(true))
      .catch(() => setIsConnected(false));

    connRef.current = conn;

    return () => {
      conn.stop().catch(() => {});
      connRef.current = null;
    };
  }, [isAuthenticated, push, guilds]);

  // Join all guild groups (so you get toasts for any guild)
  useEffect(() => {
    const conn = connRef.current;
    if (!conn || conn.state !== HubConnectionState.Connected) return;

    const ids = guilds.map((g) => g.id);
    void (async () => {
      for (const id of ids) {
        try {
          await conn.invoke("JoinGuild", id);
        } catch {
          // ignore
        }
      }
    })();
  }, [guilds, isConnected]);

  // Optional: fetch snapshot for selected guild on change
  useEffect(() => {
    if (!isAuthenticated || !selectedGuildId) return;

    void (async () => {
      const res = await fetch(`/api/music/${encodeURIComponent(selectedGuildId)}/state`, {
        credentials: "include",
        headers: { Accept: "application/json" },
      });
      if (!res.ok) return;

      const data = await res.json();
      if (data?.nowPlaying)
        setNowPlayingByGuild((prev) => {
          const existing = prev[selectedGuildId];
          const incoming = data.nowPlaying as NowPlayingPayload;
          if (existing?.track && incoming?.track) {
            const sameTrack =
              (incoming.track.uri && existing.track.uri && incoming.track.uri === existing.track.uri) ||
              incoming.track.title === existing.track.title;
            if (sameTrack && incoming.positionMs <= 0 && existing.positionMs > 0) {
              return {
                ...prev,
                [selectedGuildId]: {
                  ...incoming,
                  positionMs: existing.positionMs,
                  updatedUtc: existing.updatedUtc,
                },
              };
            }
          }

          return { ...prev, [selectedGuildId]: incoming };
        });
      if (data?.queue) setQueueByGuild((prev) => ({ ...prev, [selectedGuildId]: data.queue }));
    })();
  }, [isAuthenticated, selectedGuildId]);

  const value = useMemo<RealtimeValue>(
    () => ({ isConnected, nowPlayingByGuild, queueByGuild }),
    [isConnected, nowPlayingByGuild, queueByGuild]
  );

  return <MusicRealtimeContext.Provider value={value}>{children}</MusicRealtimeContext.Provider>;
}

export function useMusicRealtime() {
  const ctx = useContext(MusicRealtimeContext);
  if (!ctx) throw new Error("useMusicRealtime must be used within MusicRealtimeProvider.");
  return ctx;
}
