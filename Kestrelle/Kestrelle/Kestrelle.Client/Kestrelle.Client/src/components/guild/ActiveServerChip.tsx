import { useMemo } from "react";
import { useSelectedGuild } from "../../state/guild/SelectedGuildContext";

function placeholderGradient(id: string) {
  // deterministic-ish styling without assets
  const n = [...id].reduce((a, c) => a + c.charCodeAt(0), 0);
  const hue = n % 360;
  return { background: `linear-gradient(135deg, hsla(${hue}, 85%, 60%, .35), hsla(${(hue + 40) % 360}, 85%, 55%, .15))` };
}

export function ActiveServerChip() {
  const { guilds, selectedGuildId } = useSelectedGuild();

  const selected = useMemo(
    () => guilds.find((g) => g.id === selectedGuildId) ?? null,
    [guilds, selectedGuildId]
  );

  if (!selected) return null;

  return (
    <div className="inline-flex items-center gap-3 rounded-2xl border border-slate-800 bg-slate-950/40 px-3 py-2 shadow-sm shadow-slate-950/40">
      {selected.iconUrl ? (
        <img
          src={selected.iconUrl}
          alt=""
          className="h-10 w-10 rounded-xl object-cover ring-1 ring-slate-800"
        />
      ) : (
        <div
          className="h-10 w-10 rounded-xl ring-1 ring-slate-800"
          style={placeholderGradient(selected.id)}
          aria-hidden
        />
      )}
      <div className="min-w-0 max-w-[320px]">
        <div className="overflow-hidden text-sm font-semibold text-slate-100">
          <div className={`kestrelle-marquee${selected.name.length > 18 ? " is-scroll" : ""}`}>
            <span>{selected.name}</span>
          </div>
        </div>
        <div className="truncate text-xs text-slate-400">Active server</div>
      </div>
      <div className="ml-2 hidden rounded-xl border border-slate-800 bg-slate-950/60 px-2 py-1 text-[11px] text-slate-400 sm:block">
        {selected.id.slice(0, 6)}…{selected.id.slice(-4)}
      </div>
    </div>
  );
}
