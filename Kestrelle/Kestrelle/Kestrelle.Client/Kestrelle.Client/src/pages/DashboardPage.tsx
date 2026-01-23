import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { AuthOverlayGate } from "../components/auth/AuthOverlayGate";
import { RequireGuildGate } from "../components/guild/RequireGuildGate";
import { GuildSelect } from "../components/guild/GuildSelect";
import { ActiveServerChip } from "../components/guild/ActiveServerChip";
import { FeatureTabs, DashboardFeature } from "../components/dashboard/FeatureTabs";
import { useSelectedGuild } from "../state/guild/SelectedGuildContext";
import { useMusicRealtime } from "../state/music/MusicRealtimeContext";
import { useAuth } from "../state/auth/AuthContext";

function SectionCard({
  title,
  subtitle,
  children,
  headerRight,
}: {
  title: string;
  subtitle?: string;
  children: React.ReactNode;
  headerRight?: React.ReactNode;
}) {
  return (
    <div className="rounded-2xl border border-slate-800 bg-slate-950/40 p-5">
      <div className="flex items-start justify-between gap-4">
        <div>
          <div className="text-sm font-semibold">{title}</div>
          {subtitle && <div className="mt-1 text-sm text-slate-400">{subtitle}</div>}
        </div>
        {headerRight}
      </div>
      <div className="mt-4">{children}</div>
    </div>
  );
}

function msToTime(ms: number) {
  if (!ms || ms <= 0) return "0:00";
  const total = Math.floor(ms / 1000);
  const m = Math.floor(total / 60);
  const s = total % 60;
  return `${m}:${s.toString().padStart(2, "0")}`;
}

declare global {
  interface Window {
    YT: any;
    onYouTubeIframeAPIReady?: () => void;
  }
}

function extractYouTubeId(uri?: string | null) {
  if (!uri) return null;
  const match =
    uri.match(/[?&]v=([^&]+)/i) ||
    uri.match(/youtu\.be\/([^?&]+)/i) ||
    uri.match(/youtube\.com\/shorts\/([^?&]+)/i);
  return match ? match[1] : null;
}

function reorderList<T>(items: T[], fromIndex: number, toIndex: number) {
  const next = items.slice();
  const [moved] = next.splice(fromIndex, 1);
  next.splice(toIndex, 0, moved);
  return next;
}

function MusicFeature() {
  const { selectedGuildId } = useSelectedGuild();
  const { isConnected, nowPlayingByGuild, queueByGuild } = useMusicRealtime();
  const auth = useAuth();

  const now = selectedGuildId ? nowPlayingByGuild[selectedGuildId] : undefined;
  const queue = selectedGuildId ? queueByGuild[selectedGuildId] : undefined;

  const [effectiveNow, setEffectiveNow] = useState(now);

  useEffect(() => {
    if (!now) {
      setEffectiveNow(undefined);
      return;
    }

    setEffectiveNow((prev) => {
      if (!prev?.track || !now.track) return now;

      const prevTrack = prev.track;
      const nextTrack = now.track;
      const changed =
        (prevTrack.uri && nextTrack.uri && prevTrack.uri !== nextTrack.uri) ||
        prevTrack.title !== nextTrack.title;

      if (!changed) return now;

      const prevPos = prev.positionMs ?? 0;
      const prevDur = prevTrack.durationMs ?? 0;
      const nextPos = now.positionMs ?? 0;
      const nearEnd = prevDur > 0 && prevDur - prevPos < 5000;
      const allow = nextPos > 0 || prevPos === 0 || nearEnd;

      if (!allow) {
        return { ...now, track: prevTrack };
      }

      return now;
    });
  }, [now]);

  const track = effectiveNow?.track ?? null;
  const isPaused = effectiveNow?.isPaused ?? false;
  const status =
    !track ? "idle" : isPaused ? "paused" : "playing";
  const statusLabel =
    status === "idle" ? "Idle" : status === "paused" ? "Paused" : "Playing";
  const youtubeId = useMemo(() => extractYouTubeId(track?.uri), [track?.uri]);
  const coverUrl = track?.artworkUrl ?? (youtubeId ? `https://img.youtube.com/vi/${youtubeId}/hqdefault.jpg` : null);

  const [isMuted, setIsMuted] = useState(true);
  const [isPlayOpen, setIsPlayOpen] = useState(false);
  const [playQuery, setPlayQuery] = useState("");
  const playInputRef = useRef<HTMLInputElement | null>(null);

  const [displayQueue, setDisplayQueue] = useState(queue?.tracks ?? []);
  const [dragIndex, setDragIndex] = useState<number | null>(null);
  const [voiceChannels, setVoiceChannels] = useState<{ id: string; name: string }[]>([]);
  const [voiceChannelId, setVoiceChannelId] = useState<string>("");

  const [positionMs, setPositionMs] = useState(0);
  const [isSeeking, setIsSeeking] = useState(false);
  const [lastSeekSentAt, setLastSeekSentAt] = useState<number | null>(null);
  const [isYoutubeReady, setIsYoutubeReady] = useState(false);

  const playerContainerId = useMemo(
    () => (selectedGuildId ? `yt-player-${selectedGuildId}` : "yt-player"),
    [selectedGuildId]
  );
  const playerRef = useRef<any>(null);
  const lastPlayerTimeRef = useRef(0);
  const lastKnownPositionRef = useRef(0);
  const suppressPlayerStateRef = useRef(false);
  const lastControlToggleRef = useRef(0);
  const pendingResumeUntilRef = useRef(0);
  const isPausedRef = useRef(isPaused);

  const sendControl = useCallback(async (action: string, body?: Record<string, unknown>) => {
    if (!selectedGuildId) return;

    await fetch(`/api/music/guilds/${encodeURIComponent(selectedGuildId)}/controls/${action}`, {
      method: "POST",
      credentials: "include",
      headers: body ? { "Content-Type": "application/json" } : undefined,
      body: body ? JSON.stringify(body) : undefined,
    });
  }, [selectedGuildId]);

  useEffect(() => {
    setDisplayQueue(queue?.tracks ?? []);
  }, [queue?.tracks, selectedGuildId]);

  useEffect(() => {
    if (!selectedGuildId) {
      setVoiceChannels([]);
      setVoiceChannelId("");
      return;
    }

    void (async () => {
      const res = await fetch(`/api/discord/guilds/${encodeURIComponent(selectedGuildId)}/voice-channels`, {
        credentials: "include",
        headers: { Accept: "application/json" },
      });
      if (!res.ok) return;
      const data = await res.json();
      if (Array.isArray(data)) {
        setVoiceChannels(data);
        setVoiceChannelId((prev) => (prev && data.some((c) => c.id === prev) ? prev : data[0]?.id ?? ""));
      }
    })();
  }, [selectedGuildId]);

  useEffect(() => {
    setIsMuted(true);
  }, [youtubeId]);

  useEffect(() => {
    if (isPlayOpen) {
      setTimeout(() => playInputRef.current?.focus(), 50);
    }
  }, [isPlayOpen]);

  useEffect(() => {
    const rawBase = effectiveNow?.positionMs ?? 0;
    const base = rawBase > 0 ? rawBase : lastKnownPositionRef.current;
    const updatedAt = effectiveNow?.updatedUtc ? new Date(effectiveNow.updatedUtc).getTime() : Date.now();
    setPositionMs((prev) => {
      if (base > 0) return base;
      if (prev > 0) return prev;
      if (lastKnownPositionRef.current > 0) return lastKnownPositionRef.current;
      return rawBase;
    });

    const timer = setInterval(() => {
      if (!effectiveNow || !track || effectiveNow.isPaused || isSeeking) return;
      const delta = Date.now() - updatedAt;
      const next = Math.min(base + delta, track?.durationMs ?? base + delta);
      setPositionMs(next);
      lastKnownPositionRef.current = next;
    }, 1000);

    return () => clearInterval(timer);
  }, [effectiveNow?.positionMs, effectiveNow?.updatedUtc, effectiveNow?.isPaused, isSeeking, track?.durationMs]);


  useEffect(() => {
    if (positionMs > 0) lastKnownPositionRef.current = positionMs;
  }, [positionMs]);

  useEffect(() => {
    isPausedRef.current = isPaused;
  }, [isPaused]);


  useEffect(() => {
    if (!youtubeId) return;

    if (window.YT && window.YT.Player) {
      setIsYoutubeReady(true);
      return;
    }

    if (document.getElementById("yt-iframe-api")) return;

    const tag = document.createElement("script");
    tag.id = "yt-iframe-api";
    tag.src = "https://www.youtube.com/iframe_api";
    document.body.appendChild(tag);

    window.onYouTubeIframeAPIReady = () => {
      setIsYoutubeReady(true);
    };
  }, [youtubeId]);

  useEffect(() => {
    if (!youtubeId || !isYoutubeReady) return;

    if (playerRef.current) {
      playerRef.current.destroy();
      playerRef.current = null;
    }

    playerRef.current = new window.YT.Player(playerContainerId, {
      videoId: youtubeId,
      playerVars: {
        autoplay: 1,
        mute: 1,
        controls: 1,
        rel: 0,
        playsinline: 1,
      },
      events: {
        onReady: (event: any) => {
          event.target.mute();
          event.target.playVideo();
          const startMs = effectiveNow?.positionMs && effectiveNow.positionMs > 0 ? effectiveNow.positionMs : lastKnownPositionRef.current;
          if (startMs && startMs > 0) {
            event.target.seekTo(startMs / 1000, true);
          }
        },
        onStateChange: (event: any) => {
          if (suppressPlayerStateRef.current) return;
          const state = event.data;
          if (state === window.YT.PlayerState.PAUSED && !isPausedRef.current) {
            lastControlToggleRef.current = Date.now();
            void sendControl("pause");
          }
          if (state === window.YT.PlayerState.PLAYING && isPausedRef.current) {
            lastControlToggleRef.current = Date.now();
            pendingResumeUntilRef.current = Date.now() + 1500;
            void sendControl("resume");
          }
        },
      },
    });
  }, [youtubeId, playerContainerId, isYoutubeReady, sendControl]);

  useEffect(() => {
    const player = playerRef.current;
    if (!player || !track) return;

    const interval = setInterval(() => {
      if (!player || typeof player.getCurrentTime !== "function") return;

      const current = player.getCurrentTime();
      if (typeof current !== "number" || Number.isNaN(current)) return;

      const last = lastPlayerTimeRef.current;
      const delta = Math.abs(current - last);
      lastPlayerTimeRef.current = current;

      const recentlySent = lastSeekSentAt && Date.now() - lastSeekSentAt < 1500;
      if (!recentlySent && delta > 1.5) {
        const ms = Math.max(0, Math.floor(current * 1000));
        setPositionMs(ms);
        lastKnownPositionRef.current = ms;
        setLastSeekSentAt(Date.now());
        void sendControl("seek", { positionMs: ms });
      }
    }, 1000);

    return () => clearInterval(interval);
  }, [track, lastSeekSentAt, sendControl]);

  useEffect(() => {
    const player = playerRef.current;
    if (!player || !track) return;
    if (!effectiveNow?.positionMs || effectiveNow.positionMs <= 0) return;

    const recentlySent = lastSeekSentAt && Date.now() - lastSeekSentAt < 1500;
    if (recentlySent) return;

    if (typeof player.seekTo === "function") {
      suppressPlayerStateRef.current = true;
      player.seekTo(effectiveNow.positionMs / 1000, true);
      setTimeout(() => {
        suppressPlayerStateRef.current = false;
      }, 500);
    }

    setPositionMs(effectiveNow.positionMs);
    lastKnownPositionRef.current = effectiveNow.positionMs;
  }, [effectiveNow?.positionMs, track, lastSeekSentAt]);

  useEffect(() => {
    const player = playerRef.current;
    if (!player || !track) return;
    if (typeof player.pauseVideo !== "function" || typeof player.playVideo !== "function") return;

    suppressPlayerStateRef.current = true;
    if (isPaused) {
      if (Date.now() >= pendingResumeUntilRef.current) player.pauseVideo();
    } else {
      player.playVideo();
    }

    const t = setTimeout(() => {
      suppressPlayerStateRef.current = false;
    }, 500);

    return () => clearTimeout(t);
  }, [isPaused, track]);

  const progressMax = track?.durationMs ?? 0;

  const moveQueueItem = (fromIndex: number, toIndex: number) => {
    if (!selectedGuildId || fromIndex === toIndex) return;
    setDisplayQueue((prev) => reorderList(prev, fromIndex, toIndex));
    void sendControl("move-queue", { fromIndex, toIndex });
  };

  const handleSeek = (value: number) => {
    setPositionMs(value);
    lastKnownPositionRef.current = value;
    setLastSeekSentAt(Date.now());
    void sendControl("seek", { positionMs: value });
    if (playerRef.current?.seekTo) {
      playerRef.current.seekTo(value / 1000, true);
    }
  };

  const handlePlaySubmit = async () => {
    const query = playQuery.trim();
    if (!query) return;
    await sendControl("play", { query, voiceChannelId: voiceChannelId || undefined });
    setPlayQuery("");
    setIsPlayOpen(false);
  };

  useEffect(() => {
    if (!selectedGuildId) return;
    void sendControl("sync");
  }, [selectedGuildId, sendControl]);

  const handleToggle = (action: "pause" | "resume") => {
    lastControlToggleRef.current = Date.now();
    void sendControl(action);
  };


  return (
    <div className="space-y-4">
      <div className="grid gap-4 lg:grid-cols-3">
        <div className="lg:col-span-2">
          <SectionCard
            title="Now Playing"
            subtitle="Live track state and playback controls."
            headerRight={
              <span className="rounded-full border border-slate-800 bg-slate-950/60 px-2 py-1 text-[11px] text-slate-400">
                {isConnected ? "Live" : "Connecting…"}
              </span>
            }
          >
            <div className="flex flex-col gap-4">
              <div className="flex flex-col gap-4 sm:flex-row sm:items-start">
                <div className="h-28 w-28 shrink-0 overflow-hidden rounded-2xl bg-indigo-500/10 ring-1 ring-indigo-500/20">
                  {coverUrl ? <img src={coverUrl} alt="" className="h-full w-full object-cover" /> : null}
                </div>

                <div className="min-w-0 flex-1">
                <div className="overflow-hidden text-base font-semibold text-slate-100">
                  <div className={`kestrelle-marquee${track?.title && track.title.length > 28 ? " is-scroll" : ""}`}>
                    <span>{track?.title ?? "Nothing playing"}</span>
                  </div>
                </div>

                <div className="mt-1 truncate text-sm text-slate-400">
                  {track
                    ? `${track.author ?? "Unknown"} • Requested by ${
                        track.requestedBy ?? auth.user?.username ?? "unknown"
                      }`
                    : "Start playback in Discord to see live status."}
                </div>

                <div className="mt-4 space-y-3">
                  <div className="flex items-center justify-between text-xs text-slate-400">
                    <span>{msToTime(positionMs)}</span>
                    <span>{msToTime(track?.durationMs ?? 0)}</span>
                  </div>
                  <input
                    type="range"
                    min={0}
                    max={progressMax}
                    value={Math.min(positionMs, progressMax)}
                    onChange={(e) => setPositionMs(Number(e.target.value))}
                    onMouseDown={() => setIsSeeking(true)}
                    onTouchStart={() => setIsSeeking(true)}
                    onMouseUp={() => {
                      setIsSeeking(false);
                      if (progressMax > 0) handleSeek(positionMs);
                    }}
                    onTouchEnd={() => {
                      setIsSeeking(false);
                      if (progressMax > 0) handleSeek(positionMs);
                    }}
                    disabled={!track}
                    className="kestrelle-slider h-2 w-full cursor-pointer disabled:cursor-not-allowed disabled:opacity-50"
                  />
                  <div className="grid gap-2 sm:grid-cols-2">
                    <div
                      className={`flex items-center gap-3 rounded-xl border p-3 ${
                        status === "idle"
                          ? "border-slate-800 bg-slate-950/50 text-slate-300"
                          : status === "paused"
                          ? "border-amber-500/40 bg-amber-500/10 text-amber-100"
                          : "border-emerald-500/40 bg-emerald-500/10 text-emerald-100"
                      }`}
                    >
                      <div className="grid h-9 w-9 place-items-center rounded-full border border-white/10 bg-white/10">
                        {status === "idle" ? (
                          <svg viewBox="0 0 24 24" className="h-4 w-4 fill-current">
                            <path d="M6 12h12v2H6z" />
                          </svg>
                        ) : status === "paused" ? (
                          <svg viewBox="0 0 24 24" className="h-4 w-4 fill-current">
                            <path d="M6 5h4v14H6zM14 5h4v14h-4z" />
                          </svg>
                        ) : (
                          <svg viewBox="0 0 24 24" className="h-4 w-4 fill-current">
                            <path d="M8 5v14l11-7z" />
                          </svg>
                        )}
                      </div>
                      <div>
                        <div className="text-xs uppercase tracking-wide opacity-80">Status</div>
                        <div className="text-sm font-semibold">{statusLabel}</div>
                      </div>
                    </div>
                    <div className="flex items-center justify-center gap-2">
                      <button
                        className="grid h-12 w-12 place-items-center rounded-full bg-gradient-to-r from-indigo-500 to-sky-500 text-white shadow-lg shadow-indigo-500/30 transition hover:from-indigo-400 hover:to-sky-400 disabled:opacity-60"
                        disabled={!selectedGuildId}
                        onClick={() => handleToggle(isPaused ? "resume" : "pause")}
                        aria-label={isPaused ? "Play" : "Pause"}
                      >
                        {isPaused ? (
                          <svg viewBox="0 0 24 24" className="h-5 w-5 fill-current">
                            <path d="M8 5v14l11-7z" />
                          </svg>
                        ) : (
                          <svg viewBox="0 0 24 24" className="h-5 w-5 fill-current">
                            <path d="M6 5h4v14H6zM14 5h4v14h-4z" />
                          </svg>
                        )}
                      </button>
                      <button
                        className="grid h-12 w-12 place-items-center rounded-full border border-slate-700 bg-slate-950 text-slate-200 shadow-sm transition hover:border-slate-500 hover:bg-slate-900 disabled:opacity-60"
                        disabled={!selectedGuildId}
                        onClick={() => sendControl("skip")}
                        aria-label="Skip"
                      >
                        <svg viewBox="0 0 24 24" className="h-5 w-5 fill-current">
                          <path d="M6 5v14l9-7zM17 5h2v14h-2z" />
                        </svg>
                      </button>
                      <button
                        className="grid h-12 w-12 place-items-center rounded-full border border-rose-500/40 bg-rose-500/10 text-rose-200 shadow-sm transition hover:border-rose-400 hover:bg-rose-500/20 disabled:opacity-60"
                        disabled={!selectedGuildId}
                        onClick={() => sendControl("stop")}
                        aria-label="Stop"
                      >
                        <svg viewBox="0 0 24 24" className="h-5 w-5 fill-current">
                          <path d="M6 6h12v12H6z" />
                        </svg>
                      </button>
                    </div>
                  </div>
                </div>

                <div className="mt-4 flex flex-wrap gap-2" />
                </div>
              </div>
            </div>

            {youtubeId && (
              <div className="mt-4 overflow-hidden rounded-2xl border border-slate-800 bg-black">
                <div className="flex items-center justify-between border-b border-slate-800 px-3 py-2 text-xs text-slate-300">
                  <span>Now Playing Video</span>
                  <button
                    className="rounded-full border border-slate-700 bg-slate-900 px-2 py-1 text-[11px] hover:bg-slate-800"
                    onClick={() => {
                      setIsMuted((m) => {
                        const next = !m;
                        if (playerRef.current) {
                          if (next) playerRef.current.mute();
                          else playerRef.current.unMute();
                        }
                        return next;
                      });
                    }}
                  >
                    {isMuted ? "Muted" : "Sound On"}
                  </button>
                </div>
                <div className="aspect-video w-full">
                  <div id={playerContainerId} className="h-full w-full" />
                </div>
              </div>
            )}
          </SectionCard>
        </div>

        <SectionCard title="Actions" subtitle="High-impact operations (gate & audit later).">
          <div className="space-y-2">
            <div className="rounded-2xl border border-emerald-500/30 bg-emerald-500/10 p-3">
              <div className="flex items-start gap-3">
                {!isPlayOpen ? (
                  <button
                    className="grid h-10 w-10 place-items-center rounded-full bg-emerald-500 text-white shadow-lg shadow-emerald-500/30 transition hover:bg-emerald-400 disabled:opacity-60"
                    disabled={!selectedGuildId}
                    onClick={() => setIsPlayOpen(true)}
                    aria-label="Play"
                  >
                    <svg viewBox="0 0 24 24" className="h-5 w-5 fill-current">
                      <path d="M8 5v14l11-7z" />
                    </svg>
                  </button>
                ) : (
                  <button
                    className="grid h-10 w-10 place-items-center rounded-full border border-emerald-500/40 bg-emerald-500/10 text-emerald-200 transition hover:bg-emerald-500/20"
                    onClick={() => setIsPlayOpen(false)}
                    aria-label="Close"
                  >
                    ✕
                  </button>
                )}

                <div className="flex-1">
                  <div className="text-[10px] uppercase tracking-wide text-emerald-200/80">Voice Channel</div>
                  <select
                    value={voiceChannelId}
                    onChange={(e) => setVoiceChannelId(e.target.value)}
                    className="mt-2 w-full rounded-xl border border-emerald-500/30 bg-slate-950 px-3 py-2 text-sm text-slate-100 outline-none focus:border-emerald-400/60 focus:ring-2 focus:ring-emerald-400/20"
                  >
                    {voiceChannels.length === 0 && (
                      <option value="">No voice channels found</option>
                    )}
                    {voiceChannels.map((c) => (
                      <option key={c.id} value={c.id}>
                        {c.name}
                      </option>
                    ))}
                  </select>

                  <div
                    className={`mt-3 transition-all duration-300 ${
                      isPlayOpen ? "max-h-32 opacity-100" : "max-h-0 opacity-0"
                    }`}
                  >
                    <div className={`${isPlayOpen ? "block" : "hidden"}`}>
                      <input
                        ref={playInputRef}
                        value={playQuery}
                        onChange={(e) => setPlayQuery(e.target.value)}
                        onKeyDown={(e) => {
                          if (e.key === "Enter") void handlePlaySubmit();
                        }}
                        placeholder="Search or paste a YouTube link..."
                        className="w-full rounded-xl border border-emerald-500/30 bg-slate-950/70 px-3 py-2 text-sm text-slate-100 outline-none focus:border-emerald-400/60 focus:ring-2 focus:ring-emerald-400/20"
                      />
                      <button
                        className="mt-2 w-full rounded-xl bg-emerald-500 px-3 py-2 text-sm font-semibold text-white hover:bg-emerald-400"
                        disabled={!selectedGuildId}
                        onClick={() => void handlePlaySubmit()}
                      >
                        Play
                      </button>
                    </div>
                  </div>
                </div>
              </div>
            </div>

            <button
              className="w-full rounded-xl border border-slate-700 bg-slate-950 px-3 py-2 text-sm hover:bg-slate-900 disabled:opacity-60"
              disabled={!selectedGuildId}
              onClick={() => sendControl("clear-queue")}
            >
              Clear Queue
            </button>
            <button
              className="w-full rounded-xl border border-slate-700 bg-slate-950 px-3 py-2 text-sm hover:bg-slate-900 disabled:opacity-60"
              disabled={!selectedGuildId}
              onClick={() => sendControl("leave")}
            >
              Disconnect Bot
            </button>
          </div>
        </SectionCard>
      </div>

      <SectionCard title="Queue" subtitle="Upcoming tracks for the selected server.">
        <div className="space-y-2">
          {(displayQueue?.length ? displayQueue : []).map((t, idx) => (
            <div
              key={`${t.title}-${idx}`}
              draggable
              onDragStart={() => setDragIndex(idx)}
              onDragOver={(e) => e.preventDefault()}
              onDrop={() => {
                if (dragIndex === null) return;
                moveQueueItem(dragIndex, idx);
                setDragIndex(null);
              }}
              className="flex items-center justify-between gap-3 rounded-xl border border-slate-800 bg-slate-950/50 p-3 hover:border-slate-700"
            >
              <div className="flex min-w-0 items-center gap-3">
                <div className="grid h-8 w-8 place-items-center rounded-lg bg-slate-900/60 text-xs font-semibold text-slate-300 ring-1 ring-slate-800">
                  {idx + 1}
                </div>
                <div className="min-w-0">
                  <div className="truncate text-sm font-semibold text-slate-100">{t.title}</div>
                  <div className="truncate text-xs text-slate-400">
                    {t.author ? `${t.author} • ` : ""}{msToTime(t.durationMs)}
                  </div>
                </div>
              </div>
              <div className="flex items-center gap-2">
                <button
                  className="rounded-lg border border-slate-800 bg-slate-950 px-2 py-1 text-xs text-slate-200 hover:bg-slate-900 disabled:opacity-50"
                  disabled={idx === 0}
                  onClick={() => moveQueueItem(idx, idx - 1)}
                >
                  ↑
                </button>
                <button
                  className="rounded-lg border border-slate-800 bg-slate-950 px-2 py-1 text-xs text-slate-200 hover:bg-slate-900 disabled:opacity-50"
                  disabled={idx === displayQueue.length - 1}
                  onClick={() => moveQueueItem(idx, idx + 1)}
                >
                  ↓
                </button>
                <div className="rounded-lg border border-slate-800 bg-slate-950 px-2 py-1 text-xs text-slate-400">
                  ↕
                </div>
              </div>
            </div>
          ))}

          {!displayQueue?.length && (
            <div className="rounded-xl border border-slate-800 bg-slate-950/30 p-4 text-sm text-slate-400">
              Queue is empty.
            </div>
          )}
        </div>
      </SectionCard>
    </div>
  );
}

function SoundsFeature() {
  return (
    <div className="grid gap-4 lg:grid-cols-3">
      <SectionCard title="Soundboard" subtitle="Trigger short clips on demand.">
        <div className="grid grid-cols-2 gap-2 sm:grid-cols-3">
          {["Airhorn", "Bruh", "Clap", "Sad trombone", "Hype", "Rimshot"].map((s) => (
            <button
              key={s}
              className="rounded-xl border border-slate-800 bg-slate-950/50 px-3 py-2 text-sm text-slate-200 hover:bg-slate-900"
            >
              {s}
            </button>
          ))}
        </div>
      </SectionCard>

      <SectionCard title="Upload & Manage" subtitle="Future: user uploads and moderation.">
        <div className="text-sm text-slate-400">
          Placeholder for upload controls and sound management UI.
        </div>
        <div className="mt-4 space-y-2">
          <button className="w-full rounded-xl border border-slate-700 bg-slate-950 px-3 py-2 text-sm hover:bg-slate-900">
            Upload Sound (future)
          </button>
          <button className="w-full rounded-xl border border-slate-700 bg-slate-950 px-3 py-2 text-sm hover:bg-slate-900">
            Manage Sounds (future)
          </button>
        </div>
      </SectionCard>

      <SectionCard title="Permissions" subtitle="Future: per-server role controls and limits.">
        <div className="text-sm text-slate-400">
          Placeholder for role-based access and quotas.
        </div>
      </SectionCard>
    </div>
  );
}

export function DashboardPage() {
  const [feature, setFeature] = useState<DashboardFeature>("music");
  const auth = useAuth();

  return (
    <div className="space-y-5">
      {/* Header row */}
      <div className="flex flex-col gap-4 border-b border-slate-900/80 pb-4 sm:grid sm:grid-cols-[1fr_auto_1fr] sm:items-end">
        <div className="sm:justify-self-start">
          <div className="flex items-center gap-3">
            <h2 className="text-2xl font-semibold text-slate-100 tracking-tight">Dashboard</h2>
            {auth.isAuthenticated && (
              <button
                type="button"
                onClick={() => auth.signOut()}
                className="rounded-full border border-rose-500/40 bg-rose-500/15 px-3 py-1 text-[11px] font-semibold text-rose-200 shadow-sm transition hover:border-rose-400 hover:bg-rose-500/25"
              >
                Sign out
              </button>
            )}
          </div>

          <div className="mt-3">
            <ActiveServerChip />
          </div>
        </div>

        {/* Controls */}
        <div className="flex flex-col gap-3 sm:flex-row sm:items-end sm:justify-center">
          <FeatureTabs value={feature} onChange={setFeature} />
        </div>

        <div className="sm:justify-self-end">
          <GuildSelect />
        </div>
      </div>

      <AuthOverlayGate
        title="Authorize to access the dashboard"
        subtitle="Sign in with Discord to view and control your server."
      >
        <RequireGuildGate
          title="Select a server"
          subtitle="Choose a server to unlock this dashboard feature."
        >
          {feature === "music" ? <MusicFeature /> : <SoundsFeature />}
        </RequireGuildGate>
      </AuthOverlayGate>
    </div>
  );
}
