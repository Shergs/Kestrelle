import { ReactNode } from "react";
import { useSelectedGuild } from "../../state/guild/SelectedGuildContext";

export function RequireGuildGate({
  children,
  title = "Select a server to continue",
  subtitle = "Choose a Discord server to unlock controls and live status.",
}: {
  children: ReactNode;
  title?: string;
  subtitle?: string;
}) {
  const { selectedGuildId } = useSelectedGuild();

  if (selectedGuildId) {
    return <>{children}</>;
  }

  return (
    <div className="relative">
      <div className="pointer-events-none select-none blur-sm opacity-60">{children}</div>

      <div className="absolute inset-0 grid place-items-center">
        <div className="w-full max-w-md rounded-2xl border border-slate-800 bg-slate-950/80 p-6 shadow-2xl backdrop-blur">
          <div className="text-lg font-semibold">{title}</div>
          <div className="mt-1 text-sm text-slate-400">{subtitle}</div>

          <div className="mt-4 text-xs text-slate-500">
            The server dropdown is required before this feature becomes active.
          </div>
        </div>
      </div>
    </div>
  );
}
