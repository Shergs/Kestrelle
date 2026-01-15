import { useState } from "react";
import { AuthOverlayGate } from "../components/auth/AuthOverlayGate";
import { RequireGuildGate } from "../components/guild/RequireGuildGate";
import { GuildSelect } from "../components/guild/GuildSelect";
import { ActiveServerChip } from "../components/guild/ActiveServerChip";
import { FeatureTabs, DashboardFeature } from "../components/dashboard/FeatureTabs";

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

function MusicFeature() {
  return (
    <div className="space-y-4">
      {/* Top row: Now Playing (2x) + Actions (1x) */}
      <div className="grid gap-4 lg:grid-cols-3">
        <div className="lg:col-span-2">
          <SectionCard
            title="Now Playing"
            subtitle="Live track state and playback controls."
            headerRight={
              <span className="rounded-full border border-slate-800 bg-slate-950/60 px-2 py-1 text-[11px] text-slate-400">
                Live (placeholder)
              </span>
            }
          >
            <div className="flex flex-col gap-4 sm:flex-row sm:items-start">
              {/* Artwork placeholder */}
              <div className="h-28 w-28 shrink-0 rounded-2xl bg-indigo-500/10 ring-1 ring-indigo-500/20" />

              <div className="min-w-0 flex-1">
                <div className="truncate text-base font-semibold text-slate-100">
                  Track Title Placeholder
                </div>
                <div className="mt-1 truncate text-sm text-slate-400">
                  Artist / uploader placeholder • Requested by @user
                </div>

                <div className="mt-4 grid gap-2 sm:grid-cols-3">
                  <div className="rounded-xl border border-slate-800 bg-slate-950/50 p-3">
                    <div className="text-xs text-slate-400">Position</div>
                    <div className="mt-1 text-sm font-semibold">0:42</div>
                  </div>
                  <div className="rounded-xl border border-slate-800 bg-slate-950/50 p-3">
                    <div className="text-xs text-slate-400">Duration</div>
                    <div className="mt-1 text-sm font-semibold">3:31</div>
                  </div>
                  <div className="rounded-xl border border-slate-800 bg-slate-950/50 p-3">
                    <div className="text-xs text-slate-400">Volume</div>
                    <div className="mt-1 text-sm font-semibold">100%</div>
                  </div>
                </div>

                <div className="mt-4 flex flex-wrap gap-2">
                  <button className="rounded-xl bg-indigo-500 px-3 py-2 text-sm font-semibold text-white hover:bg-indigo-400">
                    Pause
                  </button>
                  <button className="rounded-xl border border-slate-700 bg-slate-950 px-3 py-2 text-sm hover:bg-slate-900">
                    Skip
                  </button>
                  <button className="rounded-xl border border-slate-700 bg-slate-950 px-3 py-2 text-sm hover:bg-slate-900">
                    Stop
                  </button>
                </div>
              </div>
            </div>
          </SectionCard>
        </div>

        <SectionCard
          title="Actions"
          subtitle="High-impact operations (gate & audit later)."
        >
          <div className="space-y-2">
            <button className="w-full rounded-xl border border-slate-700 bg-slate-950 px-3 py-2 text-sm hover:bg-slate-900">
              Clear Queue
            </button>
            <button className="w-full rounded-xl border border-slate-700 bg-slate-950 px-3 py-2 text-sm hover:bg-slate-900">
              Disconnect Bot
            </button>
            <button className="w-full rounded-xl border border-slate-700 bg-slate-950 px-3 py-2 text-sm hover:bg-slate-900">
              Move Bot to Channel (future)
            </button>
          </div>
        </SectionCard>
      </div>

      {/* Full-width Queue below */}
      <SectionCard title="Queue" subtitle="Upcoming tracks for the selected server.">
        <div className="space-y-2">
          {["Track 1", "Track 2", "Track 3", "Track 4"].map((t, idx) => (
            <div
              key={t}
              className="flex items-center justify-between gap-3 rounded-xl border border-slate-800 bg-slate-950/50 p-3"
            >
              <div className="flex min-w-0 items-center gap-3">
                <div className="grid h-8 w-8 place-items-center rounded-lg bg-slate-900/60 text-xs font-semibold text-slate-300 ring-1 ring-slate-800">
                  {idx + 1}
                </div>
                <div className="min-w-0">
                  <div className="truncate text-sm font-semibold text-slate-100">{t}</div>
                  <div className="truncate text-xs text-slate-400">3:31 • Requested by @user</div>
                </div>
              </div>

              <div className="flex items-center gap-2">
                <button className="rounded-lg border border-slate-700 bg-slate-950 px-3 py-1.5 text-xs hover:bg-slate-900">
                  Move up
                </button>
                <button className="rounded-lg border border-slate-700 bg-slate-950 px-3 py-1.5 text-xs hover:bg-slate-900">
                  Remove
                </button>
              </div>
            </div>
          ))}
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

  return (
    <div className="space-y-5">
      {/* Header row */}
      <div className="flex flex-col gap-4 sm:flex-row sm:items-end sm:justify-between">
        <div>
          <h2 className="text-xl font-semibold">Dashboard</h2>
          <p className="mt-1 text-sm text-slate-400">
            Select a server and feature to view live status and controls.
          </p>

          <div className="mt-3">
            <ActiveServerChip />
          </div>
        </div>

        {/* Controls */}
        <div className="flex flex-col gap-3 sm:flex-row sm:items-end">
          <FeatureTabs value={feature} onChange={setFeature} />
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
