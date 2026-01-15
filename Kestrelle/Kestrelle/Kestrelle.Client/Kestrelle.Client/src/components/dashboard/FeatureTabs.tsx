export type DashboardFeature = "music" | "sounds";

function TabButton({
  active,
  title,
  subtitle,
  onClick,
}: {
  active: boolean;
  title: string;
  subtitle: string;
  onClick: () => void;
}) {
  return (
    <button
      type="button"
      onClick={onClick}
      className={[
        "group relative flex flex-1 items-center gap-3 rounded-2xl px-3 py-2 text-left transition",
        "focus:outline-none focus:ring-2 focus:ring-indigo-500/25",
        active
          ? "border border-slate-700 bg-slate-950 shadow-[0_0_0_1px_rgba(15,23,42,0.35)]"
          : "border border-transparent hover:bg-slate-950/50",
      ].join(" ")}
    >
      <div
        className={[
          "grid h-9 w-9 place-items-center rounded-xl ring-1 transition",
          active
            ? "bg-indigo-500/15 text-indigo-200 ring-indigo-500/30"
            : "bg-slate-900/60 text-slate-300 ring-slate-800 group-hover:ring-slate-700",
        ].join(" ")}
      >
        {title === "Music" ? "♪" : "⟂"}
      </div>

      <div className="min-w-0">
        <div className="text-sm font-semibold text-slate-100">{title}</div>
        <div className="truncate text-xs text-slate-400">{subtitle}</div>
      </div>

      {active && (
        <div className="absolute right-3 top-3 h-2 w-2 rounded-full bg-indigo-400 shadow-[0_0_0_4px_rgba(99,102,241,0.15)]" />
      )}
    </button>
  );
}

export function FeatureTabs({
  value,
  onChange,
}: {
  value: DashboardFeature;
  onChange: (v: DashboardFeature) => void;
}) {
  return (
    <div className="min-w-[360px]">
      <div className="text-xs font-medium text-slate-400">Feature</div>
      <div className="mt-1 flex gap-2 rounded-3xl border border-slate-800 bg-slate-900/40 p-2">
        <TabButton
          active={value === "music"}
          title="Music"
          subtitle="Now playing, controls"
          onClick={() => onChange("music")}
        />
        <TabButton
          active={value === "sounds"}
          title="Sounds"
          subtitle="Soundboard & clips"
          onClick={() => onChange("sounds")}
        />
      </div>
    </div>
  );
}
