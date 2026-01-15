import { useMemo, useRef, useState } from "react";
import { useSelectedGuild } from "../../state/guild/SelectedGuildContext";

function initials(name: string) {
  const parts = name.trim().split(/\s+/).slice(0, 2);
  return parts.map((p) => p[0]?.toUpperCase()).join("");
}

export function GuildSelect() {
  const { guilds, selectedGuildId, selectGuild } = useSelectedGuild();
  const [open, setOpen] = useState(false);
  const btnRef = useRef<HTMLButtonElement | null>(null);

  const selected = useMemo(
    () => guilds.find((g) => g.id === selectedGuildId) ?? null,
    [guilds, selectedGuildId]
  );

  return (
    <div className="relative min-w-[320px]">
      <div className="flex items-center justify-between">
        <div className="text-xs font-medium text-slate-400">Server</div>
        {!selectedGuildId && (
          <span className="rounded-full border border-slate-800 bg-slate-950/60 px-2 py-0.5 text-[11px] text-slate-400">
            Required
          </span>
        )}
      </div>

      <button
        ref={btnRef}
        type="button"
        onClick={() => setOpen((v) => !v)}
        className={[
          "mt-1 flex w-full items-center justify-between gap-3 rounded-2xl border px-3 py-2.5 text-left transition",
          "bg-slate-950/60 hover:bg-slate-950 focus:outline-none focus:ring-2 focus:ring-indigo-500/25",
          selected ? "border-slate-700" : "border-slate-800",
        ].join(" ")}
      >
        <div className="flex min-w-0 items-center gap-3">
          <div className="grid h-9 w-9 shrink-0 place-items-center rounded-xl bg-slate-900/70 text-xs font-semibold text-slate-200 ring-1 ring-slate-800">
            {selected ? initials(selected.name) : "—"}
          </div>

          <div className="min-w-0">
            <div className="truncate text-sm font-semibold text-slate-100">
              {selected ? selected.name : "Select a server…"}
            </div>
            <div className="truncate text-xs text-slate-400">
              {selected ? `Guild ID: ${selected.id}` : "Choose a Discord server to activate features"}
            </div>
          </div>
        </div>

        <div className="text-slate-400">{open ? "▲" : "▼"}</div>
      </button>

      {open && (
        <>
          {/* Click-away backdrop */}
          <button
            type="button"
            className="fixed inset-0 z-40 cursor-default"
            onClick={() => setOpen(false)}
            aria-label="Close server menu"
          />

          <div className="absolute z-50 mt-2 w-full overflow-hidden rounded-2xl border border-slate-800 bg-slate-950/95 shadow-2xl backdrop-blur">
            <div className="p-2">
              {guilds.map((g) => {
                const isActive = g.id === selectedGuildId;
                return (
                  <button
                    key={g.id}
                    type="button"
                    onClick={() => {
                      selectGuild(g.id);
                      setOpen(false);
                    }}
                    className={[
                      "flex w-full items-center gap-3 rounded-xl px-3 py-2 text-left transition",
                      isActive ? "bg-indigo-500/10 ring-1 ring-indigo-500/25" : "hover:bg-slate-900/60",
                    ].join(" ")}
                  >
                    <div className="grid h-9 w-9 place-items-center rounded-xl bg-slate-900/70 text-xs font-semibold text-slate-200 ring-1 ring-slate-800">
                      {initials(g.name)}
                    </div>
                    <div className="min-w-0">
                      <div className="truncate text-sm font-semibold text-slate-100">{g.name}</div>
                      <div className="truncate text-xs text-slate-400">{g.id}</div>
                    </div>
                    {isActive && (
                      <div className="ml-auto h-2 w-2 rounded-full bg-indigo-400 shadow-[0_0_0_4px_rgba(99,102,241,0.15)]" />
                    )}
                  </button>
                );
              })}

              {selectedGuildId && (
                <button
                  type="button"
                  onClick={() => {
                    selectGuild(null);
                    setOpen(false);
                  }}
                  className="mt-2 w-full rounded-xl border border-slate-800 bg-transparent px-3 py-2 text-sm text-slate-300 hover:bg-slate-900/40"
                >
                  Clear selection
                </button>
              )}
            </div>
          </div>
        </>
      )}
    </div>
  );
}
