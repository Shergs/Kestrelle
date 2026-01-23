import { Link } from "react-router-dom";

function Pill({ children }: { children: React.ReactNode }) {
  return (
    <span className="inline-flex items-center rounded-full border border-slate-800 bg-slate-950/60 px-3 py-1 text-xs text-slate-300">
      {children}
    </span>
  );
}

function HeroStat({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-2xl border border-slate-800 bg-slate-950/40 p-4">
      <div className="text-xs text-slate-400">{label}</div>
      <div className="mt-1 text-sm font-semibold text-slate-100">{value}</div>
    </div>
  );
}

function FeatureCard({
  title,
  body,
  accent,
  items,
}: {
  title: string;
  body: string;
  accent: "indigo" | "sky" | "emerald";
  items: string[];
}) {
  const accentRing =
    accent === "emerald"
      ? "from-emerald-500/20 ring-emerald-500/20"
      : accent === "sky"
      ? "from-sky-500/20 ring-sky-500/20"
      : "from-indigo-500/20 ring-indigo-500/20";

  return (
    <div className="relative overflow-hidden rounded-3xl border border-slate-800 bg-slate-950/35 p-6">
      <div className={`pointer-events-none absolute inset-0 bg-gradient-to-br ${accentRing} via-transparent to-transparent`} />
      <div className="relative">
        <div className="text-base font-semibold text-slate-100">{title}</div>
        <div className="mt-2 text-sm text-slate-400">{body}</div>
        <ul className="mt-4 space-y-2">
          {items.map((item) => (
            <li key={item} className="flex items-start gap-2 text-sm text-slate-200/90">
              <span className="mt-1 inline-block h-1.5 w-1.5 rounded-full bg-slate-300/70" />
              <span className="leading-6">{item}</span>
            </li>
          ))}
        </ul>
      </div>
    </div>
  );
}

function HighlightCard({
  title,
  body,
  foot,
}: {
  title: string;
  body: string;
  foot: string;
}) {
  return (
    <div className="rounded-3xl border border-slate-800 bg-slate-950/40 p-6">
      <div className="text-base font-semibold text-slate-100">{title}</div>
      <div className="mt-2 text-sm text-slate-400">{body}</div>
      <div className="mt-4 rounded-2xl border border-slate-800 bg-slate-950/50 p-4 text-sm text-slate-300">
        {foot}
      </div>
    </div>
  );
}

export function OverviewPage() {
  return (
    <div className="space-y-8">
      <div className="relative overflow-hidden rounded-[32px] border border-slate-800 bg-slate-950/40 p-8">
        <div className="pointer-events-none absolute inset-0 bg-[radial-gradient(circle_at_top_left,rgba(99,102,241,0.18),transparent_55%)]" />
        <div className="relative">
          <div className="flex flex-col gap-6 lg:flex-row lg:items-end lg:justify-between">
            <div className="max-w-2xl">
              <div className="inline-flex items-center gap-2 rounded-full border border-indigo-500/30 bg-indigo-500/10 px-3 py-1 text-xs font-semibold text-indigo-200">
                Kestrelle Bot
                <span className="rounded-full border border-slate-700 bg-slate-950/70 px-2 py-0.5 text-[10px] text-slate-300">
                  Product beta
                </span>
              </div>
              <h1 className="mt-4 text-4xl font-semibold tracking-tight text-slate-100">
                The premium control plane for music in your Discord.
              </h1>
              <p className="mt-3 text-sm leading-6 text-slate-400">
                Kestrelle gives your server a professional‑grade music experience with real‑time dashboard controls,
                queue management, and a refined Now Playing view. Soundboard is next — curated clips, intro stingers,
                and reactions, all managed from the same product.
              </p>
              <div className="mt-5 flex flex-wrap gap-2">
                <a
                  href="https://discord.com/oauth2/authorize?client_id=1460833751604133928"
                  target="_blank"
                  rel="noreferrer"
                  className="rounded-xl bg-gradient-to-r from-amber-500 to-yellow-500 px-4 py-2 text-sm font-semibold text-slate-900 shadow-lg shadow-amber-500/30 transition hover:from-amber-400 hover:to-yellow-400"
                >
                  Invite Kestrelle
                </a>
                <Link
                  to="/dashboard"
                  className="rounded-xl border border-slate-700 bg-slate-950 px-4 py-2 text-sm font-semibold text-slate-100 hover:bg-slate-900"
                >
                  Open Dashboard
                </Link>
                <Link
                  to="/documentation"
                  className="rounded-xl border border-slate-700 bg-slate-950 px-4 py-2 text-sm font-semibold text-slate-100 hover:bg-slate-900"
                >
                  Product docs
                </Link>
              </div>
              <div className="mt-5 flex flex-wrap gap-2">
                <Pill>Real‑time control</Pill>
                <Pill>Queue intelligence</Pill>
                <Pill>Trusted playback</Pill>
              </div>
            </div>

            <div className="flex w-full flex-col items-center justify-center lg:w-[420px]">
              <div className="h-80 w-80 overflow-hidden rounded-[32px] border border-slate-800 bg-slate-900/40 shadow-xl shadow-slate-950/60">
                <img src="/Kestrelle_Transparent.png" alt="Kestrelle bot" className="h-full w-full object-contain" />
              </div>
            </div>
          </div>
        </div>
      </div>

      <div className="grid gap-4 lg:grid-cols-3">
        <FeatureCard
          accent="indigo"
          title="Live Music Control"
          body="Instant visibility and confident control over what your server hears."
          items={[
            "Now Playing view with synced playback state.",
            "Queue reordering, skip, stop, and realtime status updates.",
            "Designed for reliable voice playback with low latency.",
          ]}
        />
        <FeatureCard
          accent="sky"
          title="Modern Dashboard"
          body="A clean, professional interface that feels like a real product."
          items={[
            "Server‑scoped access with Discord authentication.",
            "Live queue rendering and rich track context.",
            "Built for clarity: fast, focused, and consistent.",
          ]}
        />
        <FeatureCard
          accent="emerald"
          title="Soundboard (Coming Soon)"
          body="Short clips, intros, and reactions — managed alongside music."
          items={[
            "Trigger curated clips from the dashboard.",
            "Moderation‑friendly controls and permissions.",
            "Designed for server identity and moments.",
          ]}
        />
      </div>

      <div className="grid gap-4 lg:grid-cols-2">
        <HighlightCard
          title="Built for servers that take quality seriously"
          body="Kestrelle isn’t a hobby bot. It’s a product experience for communities that want clarity, polish, and control without micromanaging commands."
          foot="Launch the dashboard to see your music state, control playback, and keep everything professional." 
        />
        <HighlightCard
          title="Roadmap: Pro + Soundboard"
          body="We’re polishing the next layer: soundboard clips, advanced queue workflows, and Pro automation."
          foot="Upgrade to Pro is coming soon — sign up inside the app to stay in the loop."
        />
      </div>

      <div className="rounded-3xl border border-slate-800 bg-slate-950/40 p-7">
        <div className="flex flex-col gap-4 lg:flex-row lg:items-center lg:justify-between">
          <div>
            <div className="text-base font-semibold text-slate-100">Ready to launch?</div>
            <div className="mt-1 text-sm text-slate-400">
              Choose a server and take control of your music experience.
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
              Product docs
            </Link>
          </div>
        </div>
      </div>
    </div>
  );
}
