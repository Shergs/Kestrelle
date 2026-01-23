export function DocumentationPage() {
  return (
    <div className="space-y-6">
      <div>
        <h2 className="text-2xl font-semibold text-slate-100">Documentation</h2>
        <p className="mt-1 text-sm text-slate-400">Kestrelle bot commands and product notes.</p>
      </div>

      <div className="rounded-3xl border border-slate-800 bg-slate-950/40 p-6">
        <div className="text-base font-semibold text-slate-100">Music Commands</div>
        <div className="mt-1 text-sm text-slate-400">Slash commands for live music control.</div>

        <div className="mt-4 grid gap-3 lg:grid-cols-2">
          {[
            { cmd: "/play <query>", desc: "Searches YouTube and starts playback." },
            { cmd: "/pause", desc: "Pauses the current track." },
            { cmd: "/resume", desc: "Resumes playback." },
            { cmd: "/skip", desc: "Skips the current track." },
            { cmd: "/stop", desc: "Stops playback and clears the queue." },
            { cmd: "/leave", desc: "Disconnects the bot from voice." },
          ].map((c) => (
            <div
              key={c.cmd}
              className="flex items-center justify-between rounded-2xl border border-slate-800 bg-slate-950/50 px-4 py-3"
            >
              <div className="text-sm font-semibold text-slate-100">{c.cmd}</div>
              <div className="text-xs text-slate-400">{c.desc}</div>
            </div>
          ))}
        </div>
      </div>

      <div className="rounded-3xl border border-slate-800 bg-slate-950/40 p-6">
        <div className="text-base font-semibold text-slate-100">Sounds</div>
        <div className="mt-2 rounded-2xl border border-slate-800 bg-slate-950/60 p-6 text-sm text-slate-300 blur-[2px]">
          Soundboard clips, intro stingers, and reaction moments — coming soon.
        </div>
        <div className="mt-2 text-xs text-slate-500">Coming soon</div>
      </div>
    </div>
  );
}
