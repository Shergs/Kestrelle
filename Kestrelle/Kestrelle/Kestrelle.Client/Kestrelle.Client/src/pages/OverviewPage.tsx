import { Link } from "react-router-dom";

function GradientBadge({ children }: { children: React.ReactNode }) {
  return (
    <span className="inline-flex items-center rounded-full border border-indigo-500/25 bg-indigo-500/10 px-3 py-1 text-xs font-semibold text-indigo-200">
      {children}
    </span>
  );
}

function Stat({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-2xl border border-slate-800 bg-slate-950/40 p-4">
      <div className="text-xs text-slate-400">{label}</div>
      <div className="mt-1 text-sm font-semibold text-slate-100">{value}</div>
    </div>
  );
}

function FeatureCard({
  title,
  subtitle,
  bullets,
  accent = "indigo",
  footer,
}: {
  title: string;
  subtitle: string;
  bullets: string[];
  accent?: "indigo" | "emerald" | "sky";
  footer?: React.ReactNode;
}) {
  const accentStyles =
    accent === "emerald"
      ? "from-emerald-500/20 via-transparent to-transparent ring-emerald-500/20"
      : accent === "sky"
      ? "from-sky-500/20 via-transparent to-transparent ring-sky-500/20"
      : "from-indigo-500/20 via-transparent to-transparent ring-indigo-500/20";

  return (
    <div className="relative overflow-hidden rounded-2xl border border-slate-800 bg-slate-950/35 p-6">
      <div className={`pointer-events-none absolute inset-0 bg-gradient-to-br ${accentStyles}`} />
      <div className="relative">
        <div className="text-base font-semibold text-slate-100">{title}</div>
        <div className="mt-1 text-sm text-slate-400">{subtitle}</div>

        <ul className="mt-4 space-y-2">
          {bullets.map((b) => (
            <li key={b} className="flex items-start gap-2 text-sm text-slate-200/90">
              <span className="mt-1 inline-block h-1.5 w-1.5 rounded-full bg-slate-300/70" />
              <span className="leading-6">{b}</span>
            </li>
          ))}
        </ul>

        {footer && <div className="mt-5">{footer}</div>}
      </div>
    </div>
  );
}

function TimelineStep({
  n,
  title,
  body,
}: {
  n: string;
  title: string;
  body: string;
}) {
  return (
    <div className="flex gap-4">
      <div className="grid h-10 w-10 shrink-0 place-items-center rounded-2xl border border-slate-800 bg-slate-950/60 text-sm font-semibold text-slate-100">
        {n}
      </div>
      <div className="min-w-0">
        <div className="text-sm font-semibold text-slate-100">{title}</div>
        <div className="mt-1 text-sm text-slate-400">{body}</div>
      </div>
    </div>
  );
}

export function OverviewPage() {
  return (
    <div className="space-y-8">
      {/* Hero */}
      <div className="relative overflow-hidden rounded-3xl border border-slate-800 bg-slate-950/35 p-8">
        <div className="pointer-events-none absolute inset-0 bg-gradient-to-br from-indigo-500/15 via-transparent to-transparent" />
        <div className="relative">
          <div className="flex flex-col gap-4 lg:flex-row lg:items-end lg:justify-between">
            <div className="max-w-2xl">
              <GradientBadge>Discord Music Bot + Web Control Plane</GradientBadge>

              <h1 className="mt-4 text-3xl font-semibold tracking-tight text-slate-100">
                Kestrelle: music in voice, control in the browser
              </h1>

              <p className="mt-3 text-sm leading-6 text-slate-400">
                Kestrelle is a lightweight Discord music bot powered by Lavalink, paired with a modern web
                dashboard for live status, queue visibility, and richer controls. Add a soundboard for quick
                reactions, intros, and server “moments.”
              </p>

              <div className="mt-5 flex flex-wrap gap-2">
                <Link
                  to="/dashboard"
                  className="rounded-xl bg-indigo-500 px-4 py-2 text-sm font-semibold text-white hover:bg-indigo-400"
                >
                  Open Dashboard
                </Link>
                <Link
                  to="/documentation"
                  className="rounded-xl border border-slate-700 bg-slate-950 px-4 py-2 text-sm font-semibold text-slate-100 hover:bg-slate-900"
                >
                  View Docs
                </Link>
              </div>
            </div>

            <div className="grid w-full gap-3 lg:w-[420px] lg:grid-cols-2">
              <Stat label="Playback Engine" value="Lavalink" />
              <Stat label="UI Stack" value="React + TypeScript + Tailwind" />
              <Stat label="API Stack" value=".NET 10 Minimal API" />
              <Stat label="Deploy Model" value="Docker Compose (modular)" />
            </div>
          </div>
        </div>
      </div>

      {/* Two big pillars */}
      <div className="grid gap-4 lg:grid-cols-2">
        <FeatureCard
          accent="indigo"
          title="Music Bot"
          subtitle="Voice-first playback with predictable controls."
          bullets={[
            "Slash commands for play, pause, skip, stop, and queue management (expanding).",
            "Lavalink-backed YouTube search/play with plugin support (configurable).",
            "Clean operational boundaries: bot handles Discord + voice; Lavalink handles audio pipeline.",
            "Designed for containerized local development and future production deployment.",
          ]}
          footer={
            <div className="flex flex-wrap gap-2">
              <span className="rounded-full border border-slate-800 bg-slate-950/60 px-3 py-1 text-xs text-slate-300">
                Low-latency voice
              </span>
              <span className="rounded-full border border-slate-800 bg-slate-950/60 px-3 py-1 text-xs text-slate-300">
                Reliable queueing
              </span>
              <span className="rounded-full border border-slate-800 bg-slate-950/60 px-3 py-1 text-xs text-slate-300">
                Docker-friendly
              </span>
            </div>
          }
        />

        <FeatureCard
          accent="sky"
          title="Web Dashboard"
          subtitle="A modern control plane for live status and richer interactions."
          bullets={[
            "Authenticate with Discord (planned) to discover and select servers you manage.",
            "Live ‘Now Playing’ status and queue visualization for the selected server.",
            "Modern UI patterns: feature switching (Music/Sounds) and server-scoped gating.",
            "Ready to extend: moderation, audit logs, role-based permissions, and scheduling.",
          ]}
          footer={
            <div className="flex flex-wrap gap-2">
              <span className="rounded-full border border-slate-800 bg-slate-950/60 px-3 py-1 text-xs text-slate-300">
                Server-scoped controls
              </span>
              <span className="rounded-full border border-slate-800 bg-slate-950/60 px-3 py-1 text-xs text-slate-300">
                Clean routing
              </span>
              <span className="rounded-full border border-slate-800 bg-slate-950/60 px-3 py-1 text-xs text-slate-300">
                Professional UX
              </span>
            </div>
          }
        />
      </div>

      {/* Soundboard highlight */}
      <FeatureCard
        accent="emerald"
        title="Sounds / Soundboard"
        subtitle="Short clips on demand for reactions, intros, and fun server interactions."
        bullets={[
          "Trigger curated clips from the dashboard (future) or via commands.",
          "Designed for future user uploads with EF-backed metadata and storage paths.",
          "Server-level permissions, rate limiting, and moderation-friendly controls (planned).",
        ]}
        footer={
          <div className="rounded-2xl border border-slate-800 bg-slate-950/50 p-4">
            <div className="text-sm font-semibold text-slate-100">Why it matters</div>
            <div className="mt-1 text-sm text-slate-400">
              Sounds let you build a “server identity” beyond music—quick moments, lightweight reactions,
              and curated clips without spamming music queues.
            </div>
          </div>
        }
      />

      {/* How it works */}
      <div className="grid gap-4 lg:grid-cols-3">
        <div className="lg:col-span-2 rounded-3xl border border-slate-800 bg-slate-950/35 p-6">
          <div className="text-base font-semibold text-slate-100">How it works</div>
          <div className="mt-1 text-sm text-slate-400">
            A clean separation of responsibilities makes deployments predictable and extensions safe.
          </div>

          <div className="mt-5 space-y-5">
            <TimelineStep
              n="1"
              title="User interacts in Discord or the Dashboard"
              body="Slash commands handle voice-first control, while the dashboard provides live visibility and richer UI workflows."
            />
            <TimelineStep
              n="2"
              title="Bot manages Discord state + voice connections"
              body="The bot handles guild context, voice state, and player orchestration while remaining lightweight and resilient."
            />
            <TimelineStep
              n="3"
              title="Lavalink handles audio playback pipeline"
              body="Search/track loading and audio streaming are delegated to Lavalink for performance and stability."
            />
          </div>
        </div>

        <div className="rounded-3xl border border-slate-800 bg-slate-950/35 p-6">
          <div className="text-base font-semibold text-slate-100">Architecture (local)</div>
          <div className="mt-1 text-sm text-slate-400">
            Optimized for Docker Compose now, portable to future deployments later.
          </div>

          <div className="mt-4 space-y-3">
            {[
              { name: "kestrelle-client", desc: "React UI (nginx)" },
              { name: "kestrelle-api", desc: ".NET Minimal API" },
              { name: "kestrelle-bot", desc: "Discord bot worker" },
              { name: "lavalink", desc: "Audio playback engine" },
              { name: "db", desc: "Postgres (EF Core)" },
            ].map((x) => (
              <div
                key={x.name}
                className="flex items-center justify-between rounded-2xl border border-slate-800 bg-slate-950/50 px-4 py-3"
              >
                <div className="text-sm font-semibold text-slate-100">{x.name}</div>
                <div className="text-xs text-slate-400">{x.desc}</div>
              </div>
            ))}
          </div>

          <div className="mt-4 rounded-2xl border border-slate-800 bg-slate-950/50 p-4 text-sm text-slate-400">
            Next step: Discord OAuth to populate servers dynamically and scope dashboard access by role.
          </div>
        </div>
      </div>

      {/* CTA */}
      <div className="rounded-3xl border border-slate-800 bg-slate-950/35 p-7">
        <div className="flex flex-col gap-4 lg:flex-row lg:items-center lg:justify-between">
          <div>
            <div className="text-base font-semibold text-slate-100">Ready to explore?</div>
            <div className="mt-1 text-sm text-slate-400">
              Open the dashboard and select a server to unlock Music and Sounds controls.
            </div>
          </div>
          <div className="flex flex-wrap gap-2">
            <Link
              to="/dashboard"
              className="rounded-xl bg-indigo-500 px-4 py-2 text-sm font-semibold text-white hover:bg-indigo-400"
            >
              Go to Dashboard
            </Link>
            <Link
              to="/documentation"
              className="rounded-xl border border-slate-700 bg-slate-950 px-4 py-2 text-sm font-semibold text-slate-100 hover:bg-slate-900"
            >
              Documentation
            </Link>
          </div>
        </div>
      </div>
    </div>
  );
}