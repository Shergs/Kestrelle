import { FormEvent, useEffect, useMemo, useState } from "react";
import { useSelectedGuild } from "../../state/guild/SelectedGuildContext";
import { useToast } from "../toast/ToastContext";

type SoundSummary = {
  id: string;
  displayName: string;
  trigger: string;
  originalFileName: string;
  contentType: string;
  sizeBytes: number;
  durationMs: number;
  uploadedByUsername: string;
  contentUrl: string;
  createdUtc: string;
  updatedUtc: string;
};

type VoiceChannel = {
  id: string;
  name: string;
};

function SectionCard({
  title,
  subtitle,
  children,
}: {
  title: string;
  subtitle?: string;
  children: React.ReactNode;
}) {
  return (
    <div className="rounded-2xl border border-slate-800 bg-slate-950/40 p-5">
      <div>
        <div className="text-sm font-semibold">{title}</div>
        {subtitle && <div className="mt-1 text-sm text-slate-400">{subtitle}</div>}
      </div>
      <div className="mt-4">{children}</div>
    </div>
  );
}

function formatDuration(durationMs: number) {
  if (!durationMs || durationMs <= 0) return "0:00";
  const totalSeconds = Math.floor(durationMs / 1000);
  const minutes = Math.floor(totalSeconds / 60);
  const seconds = totalSeconds % 60;
  return `${minutes}:${seconds.toString().padStart(2, "0")}`;
}

function formatBytes(sizeBytes: number) {
  if (sizeBytes < 1024) return `${sizeBytes} B`;
  if (sizeBytes < 1024 * 1024) return `${(sizeBytes / 1024).toFixed(1)} KB`;
  return `${(sizeBytes / (1024 * 1024)).toFixed(1)} MB`;
}

function slugify(value: string) {
  return value
    .trim()
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, "-")
    .replace(/^-+|-+$/g, "")
    .slice(0, 64);
}

async function parseError(response: Response) {
  const text = await response.text().catch(() => "");
  if (!text) return `Request failed (${response.status}).`;

  try {
    const json = JSON.parse(text) as { error?: string };
    return json.error ?? text;
  } catch {
    return text;
  }
}

export function SoundsFeaturePanel() {
  const { selectedGuildId } = useSelectedGuild();
  const { push } = useToast();

  const [sounds, setSounds] = useState<SoundSummary[]>([]);
  const [voiceChannels, setVoiceChannels] = useState<VoiceChannel[]>([]);
  const [voiceChannelId, setVoiceChannelId] = useState("");
  const [search, setSearch] = useState("");
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [uploadFile, setUploadFile] = useState<File | null>(null);
  const [uploadDisplayName, setUploadDisplayName] = useState("");
  const [uploadTrigger, setUploadTrigger] = useState("");
  const [triggerTouched, setTriggerTouched] = useState(false);
  const [uploadProgress, setUploadProgress] = useState<number | null>(null);
  const [isUploading, setIsUploading] = useState(false);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [editDisplayName, setEditDisplayName] = useState("");
  const [editTrigger, setEditTrigger] = useState("");
  const [editTriggerTouched, setEditTriggerTouched] = useState(false);
  const [isSavingEdit, setIsSavingEdit] = useState(false);
  const [deletingId, setDeletingId] = useState<string | null>(null);
  const [playingId, setPlayingId] = useState<string | null>(null);

  const filteredSounds = useMemo(() => {
    const query = search.trim().toLowerCase();
    if (!query) return sounds;

    return sounds.filter((sound) =>
      sound.displayName.toLowerCase().includes(query) ||
      sound.trigger.toLowerCase().includes(query) ||
      sound.uploadedByUsername.toLowerCase().includes(query)
    );
  }, [search, sounds]);

  useEffect(() => {
    if (!selectedGuildId) {
      setSounds([]);
      setError(null);
      return;
    }

    let cancelled = false;
    setIsLoading(true);
    setError(null);

    void (async () => {
      const response = await fetch(`/api/sounds/guilds/${encodeURIComponent(selectedGuildId)}`, {
        credentials: "include",
        headers: { Accept: "application/json" },
      });

      if (cancelled) return;

      if (!response.ok) {
        setSounds([]);
        setError(await parseError(response));
        setIsLoading(false);
        return;
      }

      const data = (await response.json()) as SoundSummary[];
      setSounds(Array.isArray(data) ? data : []);
      setIsLoading(false);
    })();

    return () => {
      cancelled = true;
    };
  }, [selectedGuildId]);

  useEffect(() => {
    if (!selectedGuildId) {
      setVoiceChannels([]);
      setVoiceChannelId("");
      return;
    }

    let cancelled = false;

    void (async () => {
      const response = await fetch(`/api/discord/guilds/${encodeURIComponent(selectedGuildId)}/voice-channels`, {
        credentials: "include",
        headers: { Accept: "application/json" },
      });

      if (cancelled || !response.ok) return;

      const data = (await response.json()) as VoiceChannel[];
      if (!Array.isArray(data)) return;

      setVoiceChannels(data);
      setVoiceChannelId((current) => (current && data.some((channel) => channel.id === current) ? current : data[0]?.id ?? ""));
    })();

    return () => {
      cancelled = true;
    };
  }, [selectedGuildId]);

  const refreshSounds = async () => {
    if (!selectedGuildId) return;

    setIsLoading(true);
    setError(null);

    const response = await fetch(`/api/sounds/guilds/${encodeURIComponent(selectedGuildId)}`, {
      credentials: "include",
      headers: { Accept: "application/json" },
    });

    if (!response.ok) {
      setError(await parseError(response));
      setIsLoading(false);
      return;
    }

    const data = (await response.json()) as SoundSummary[];
    setSounds(Array.isArray(data) ? data : []);
    setIsLoading(false);
  };

  const resetUploadForm = () => {
    setUploadFile(null);
    setUploadDisplayName("");
    setUploadTrigger("");
    setTriggerTouched(false);
    setUploadProgress(null);
  };

  const handleUpload = async (event: FormEvent) => {
    event.preventDefault();
    if (!selectedGuildId || !uploadFile) return;

    const displayName = uploadDisplayName.trim();
    const trigger = slugify(uploadTrigger || uploadDisplayName);

    if (!displayName) {
      push({ kind: "warning", title: "Upload", message: "Display name is required." });
      return;
    }

    if (!trigger) {
      push({ kind: "warning", title: "Upload", message: "Trigger is required." });
      return;
    }

    const formData = new FormData();
    formData.append("file", uploadFile);
    formData.append("displayName", displayName);
    formData.append("trigger", trigger);

    setIsUploading(true);
    setUploadProgress(0);

    try {
      const created = await new Promise<SoundSummary>((resolve, reject) => {
        const xhr = new XMLHttpRequest();
        xhr.open("POST", `/api/sounds/guilds/${encodeURIComponent(selectedGuildId)}`);
        xhr.withCredentials = true;

        xhr.upload.onprogress = (progress) => {
          if (!progress.lengthComputable) return;
          setUploadProgress(Math.round((progress.loaded / progress.total) * 100));
        };

        xhr.onload = () => {
          const responseText = xhr.responseText || "";
          if (xhr.status >= 200 && xhr.status < 300) {
            try {
              resolve(JSON.parse(responseText) as SoundSummary);
            } catch {
              reject(new Error("Upload succeeded but the server response was invalid."));
            }
            return;
          }

          try {
            const json = JSON.parse(responseText) as { error?: string };
            reject(new Error(json.error ?? (responseText || `Upload failed (${xhr.status}).`)));
          } catch {
            reject(new Error(responseText || `Upload failed (${xhr.status}).`));
          }
        };

        xhr.onerror = () => reject(new Error("Upload failed due to a network error."));
        xhr.send(formData);
      });

      setSounds((current) => [...current, created].sort((a, b) => a.displayName.localeCompare(b.displayName)));
      resetUploadForm();
      push({ kind: "success", title: "Sound uploaded", message: `${created.displayName} is ready to play.` });
    } catch (uploadError) {
      const message = uploadError instanceof Error ? uploadError.message : "Upload failed.";
      push({ kind: "error", title: "Upload failed", message });
    } finally {
      setIsUploading(false);
      setUploadProgress(null);
    }
  };

  const handlePlay = async (sound: SoundSummary) => {
    if (!selectedGuildId) return;

    setPlayingId(sound.id);
    try {
      const response = await fetch(`/api/sounds/guilds/${encodeURIComponent(selectedGuildId)}/${sound.id}/play`, {
        method: "POST",
        credentials: "include",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ voiceChannelId: voiceChannelId || undefined }),
      });

      if (!response.ok) {
        push({ kind: "error", title: "Playback failed", message: await parseError(response) });
        return;
      }

      push({ kind: "success", title: "Soundboard", message: `Triggered ${sound.displayName}.` });
    } finally {
      setPlayingId(null);
    }
  };

  const beginEdit = (sound: SoundSummary) => {
    setEditingId(sound.id);
    setEditDisplayName(sound.displayName);
    setEditTrigger(sound.trigger);
    setEditTriggerTouched(false);
  };

  const handleSaveEdit = async (soundId: string) => {
    if (!selectedGuildId) return;

    const displayName = editDisplayName.trim();
    const trigger = slugify(editTrigger || editDisplayName);

    if (!displayName || !trigger) {
      push({ kind: "warning", title: "Edit sound", message: "Display name and trigger are required." });
      return;
    }

    setIsSavingEdit(true);
    try {
      const response = await fetch(`/api/sounds/guilds/${encodeURIComponent(selectedGuildId)}/${soundId}`, {
        method: "PATCH",
        credentials: "include",
        headers: {
          "Content-Type": "application/json",
          Accept: "application/json",
        },
        body: JSON.stringify({ displayName, trigger }),
      });

      if (!response.ok) {
        push({ kind: "error", title: "Edit failed", message: await parseError(response) });
        return;
      }

      const updated = (await response.json()) as SoundSummary;
      setSounds((current) => current.map((sound) => (sound.id === updated.id ? updated : sound)).sort((a, b) => a.displayName.localeCompare(b.displayName)));
      setEditingId(null);
      push({ kind: "success", title: "Sound updated", message: `${updated.displayName} was updated.` });
    } finally {
      setIsSavingEdit(false);
    }
  };

  const handleDelete = async (sound: SoundSummary) => {
    if (!selectedGuildId) return;
    const confirmed = window.confirm(`Delete ${sound.displayName}? This removes the sound file for the guild.`);
    if (!confirmed) return;

    setDeletingId(sound.id);
    try {
      const response = await fetch(`/api/sounds/guilds/${encodeURIComponent(selectedGuildId)}/${sound.id}`, {
        method: "DELETE",
        credentials: "include",
      });

      if (!response.ok) {
        push({ kind: "error", title: "Delete failed", message: await parseError(response) });
        return;
      }

      setSounds((current) => current.filter((item) => item.id !== sound.id));
      push({ kind: "success", title: "Sound deleted", message: `${sound.displayName} was removed.` });
    } finally {
      setDeletingId(null);
    }
  };

  return (
    <div className="space-y-4">
      <div className="grid gap-4 lg:grid-cols-[1.6fr_1fr]">
        <SectionCard title="Soundboard" subtitle="Guild-scoped clips you can trigger instantly from the dashboard.">
          <div className="flex flex-col gap-3">
            <div className="grid gap-3 md:grid-cols-[1fr_220px]">
              <input
                value={search}
                onChange={(event) => setSearch(event.target.value)}
                placeholder="Search sounds, triggers, or uploaders..."
                className="rounded-xl border border-slate-700 bg-slate-950/70 px-3 py-2 text-sm text-slate-100 outline-none focus:border-indigo-400/60 focus:ring-2 focus:ring-indigo-400/20"
              />
              <select
                value={voiceChannelId}
                onChange={(event) => setVoiceChannelId(event.target.value)}
                className="rounded-xl border border-slate-700 bg-slate-950/70 px-3 py-2 text-sm text-slate-100 outline-none focus:border-indigo-400/60 focus:ring-2 focus:ring-indigo-400/20"
              >
                {voiceChannels.length === 0 && <option value="">No voice channels</option>}
                {voiceChannels.map((channel) => (
                  <option key={channel.id} value={channel.id}>
                    {channel.name}
                  </option>
                ))}
              </select>
            </div>

            <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-3">
              {filteredSounds.map((sound) => (
                <button
                  key={sound.id}
                  type="button"
                  onClick={() => void handlePlay(sound)}
                  disabled={!voiceChannelId || playingId === sound.id}
                  className="rounded-2xl border border-slate-800 bg-slate-950/50 p-4 text-left transition hover:border-indigo-500/40 hover:bg-slate-900 disabled:cursor-not-allowed disabled:opacity-50"
                >
                  <div className="flex items-start justify-between gap-3">
                    <div>
                      <div className="text-sm font-semibold text-slate-100">{sound.displayName}</div>
                      <div className="mt-1 text-xs text-slate-400">/{sound.trigger}</div>
                    </div>
                    <div className="grid h-10 w-10 place-items-center rounded-full bg-gradient-to-r from-indigo-500 to-sky-500 text-white shadow-lg shadow-indigo-500/30">
                      {playingId === sound.id ? "…" : "▶"}
                    </div>
                  </div>
                  <div className="mt-3 flex items-center justify-between text-xs text-slate-400">
                    <span>{formatDuration(sound.durationMs)}</span>
                    <span>{formatBytes(sound.sizeBytes)}</span>
                  </div>
                </button>
              ))}
            </div>

            {!isLoading && filteredSounds.length === 0 && (
              <div className="rounded-xl border border-dashed border-slate-800 bg-slate-950/30 p-4 text-sm text-slate-400">
                {sounds.length === 0
                  ? "No sounds uploaded for this guild yet. Use the upload panel to create the soundboard."
                  : "No sounds match that search."}
              </div>
            )}

            {isLoading && (
              <div className="rounded-xl border border-slate-800 bg-slate-950/30 p-4 text-sm text-slate-400">Loading sounds…</div>
            )}

            {error && (
              <div className="rounded-xl border border-rose-500/30 bg-rose-500/10 p-4 text-sm text-rose-100">{error}</div>
            )}
          </div>
        </SectionCard>

        <SectionCard title="Upload Sound" subtitle="Short clips only: mp3, wav, ogg, or m4a. Max 10 seconds and 5 MiB.">
          <form className="space-y-3" onSubmit={(event) => void handleUpload(event)}>
            <label className="block text-xs uppercase tracking-wide text-slate-400">
              Audio file
              <input
                type="file"
                accept=".mp3,.wav,.ogg,.m4a,audio/*"
                onChange={(event) => {
                  const nextFile = event.target.files?.[0] ?? null;
                  setUploadFile(nextFile);

                  if (!nextFile) return;

                  const fallbackName = nextFile.name.replace(/\.[^.]+$/, "");
                  setUploadDisplayName((current) => current || fallbackName);
                  if (!triggerTouched) {
                    setUploadTrigger((current) => current || slugify(fallbackName));
                  }
                }}
                className="mt-2 block w-full rounded-xl border border-slate-700 bg-slate-950/70 px-3 py-2 text-sm text-slate-200 file:mr-3 file:rounded-lg file:border-0 file:bg-slate-800 file:px-3 file:py-2 file:text-sm file:font-semibold file:text-slate-100"
              />
            </label>

            <label className="block text-xs uppercase tracking-wide text-slate-400">
              Display name
              <input
                value={uploadDisplayName}
                onChange={(event) => {
                  const value = event.target.value;
                  setUploadDisplayName(value);
                  if (!triggerTouched) {
                    setUploadTrigger(slugify(value));
                  }
                }}
                placeholder="Airhorn"
                className="mt-2 w-full rounded-xl border border-slate-700 bg-slate-950/70 px-3 py-2 text-sm text-slate-100 outline-none focus:border-emerald-400/60 focus:ring-2 focus:ring-emerald-400/20"
              />
            </label>

            <label className="block text-xs uppercase tracking-wide text-slate-400">
              Trigger
              <input
                value={uploadTrigger}
                onChange={(event) => {
                  setTriggerTouched(true);
                  setUploadTrigger(slugify(event.target.value));
                }}
                placeholder="airhorn"
                className="mt-2 w-full rounded-xl border border-slate-700 bg-slate-950/70 px-3 py-2 text-sm text-slate-100 outline-none focus:border-emerald-400/60 focus:ring-2 focus:ring-emerald-400/20"
              />
            </label>

            <div className="rounded-xl border border-slate-800 bg-slate-950/50 p-3 text-xs text-slate-400">
              Dashboard playback uses the voice channel selector on the left. Discord slash command playback uses the caller’s active voice channel.
            </div>

            {uploadProgress !== null && (
              <div className="space-y-2">
                <div className="h-2 overflow-hidden rounded-full bg-slate-900">
                  <div className="h-full rounded-full bg-emerald-500 transition-all" style={{ width: `${uploadProgress}%` }} />
                </div>
                <div className="text-xs text-slate-400">Uploading… {uploadProgress}%</div>
              </div>
            )}

            <div className="flex gap-2">
              <button
                type="submit"
                disabled={!uploadFile || isUploading}
                className="flex-1 rounded-xl bg-emerald-500 px-3 py-2 text-sm font-semibold text-white transition hover:bg-emerald-400 disabled:cursor-not-allowed disabled:opacity-60"
              >
                {isUploading ? "Uploading…" : "Upload sound"}
              </button>
              <button
                type="button"
                onClick={resetUploadForm}
                disabled={isUploading && uploadProgress !== null}
                className="rounded-xl border border-slate-700 bg-slate-950 px-3 py-2 text-sm text-slate-100 transition hover:bg-slate-900"
              >
                Reset
              </button>
            </div>
          </form>
        </SectionCard>
      </div>

      <SectionCard title="Manage Sounds" subtitle="Preview, edit, and delete uploaded clips for this guild.">
        <div className="space-y-3">
          {sounds.map((sound) => {
            const isEditing = editingId === sound.id;
            return (
              <div key={sound.id} className="rounded-2xl border border-slate-800 bg-slate-950/45 p-4">
                <div className="grid gap-4 lg:grid-cols-[1.3fr_220px] lg:items-center">
                  <div className="space-y-3">
                    {isEditing ? (
                      <>
                        <input
                          value={editDisplayName}
                          onChange={(event) => {
                            const value = event.target.value;
                            setEditDisplayName(value);
                            if (!editTriggerTouched) setEditTrigger(slugify(value));
                          }}
                          className="w-full rounded-xl border border-slate-700 bg-slate-950 px-3 py-2 text-sm text-slate-100 outline-none focus:border-indigo-400/60 focus:ring-2 focus:ring-indigo-400/20"
                        />
                        <input
                          value={editTrigger}
                          onChange={(event) => {
                            setEditTriggerTouched(true);
                            setEditTrigger(slugify(event.target.value));
                          }}
                          className="w-full rounded-xl border border-slate-700 bg-slate-950 px-3 py-2 text-sm text-slate-100 outline-none focus:border-indigo-400/60 focus:ring-2 focus:ring-indigo-400/20"
                        />
                      </>
                    ) : (
                      <div>
                        <div className="text-sm font-semibold text-slate-100">{sound.displayName}</div>
                        <div className="mt-1 text-xs text-slate-400">
                          Trigger: <span className="font-medium text-slate-200">{sound.trigger}</span> • Uploaded by {sound.uploadedByUsername}
                        </div>
                      </div>
                    )}

                    <div className="flex flex-wrap gap-3 text-xs text-slate-400">
                      <span>{sound.originalFileName}</span>
                      <span>{formatDuration(sound.durationMs)}</span>
                      <span>{formatBytes(sound.sizeBytes)}</span>
                    </div>
                  </div>

                  <div className="space-y-3">
                    <audio controls preload="none" src={sound.contentUrl} className="w-full" />
                    <div className="flex flex-wrap gap-2">
                      {isEditing ? (
                        <>
                          <button
                            type="button"
                            onClick={() => void handleSaveEdit(sound.id)}
                            disabled={isSavingEdit}
                            className="rounded-xl bg-indigo-500 px-3 py-2 text-sm font-semibold text-white transition hover:bg-indigo-400 disabled:opacity-60"
                          >
                            {isSavingEdit ? "Saving…" : "Save"}
                          </button>
                          <button
                            type="button"
                            onClick={() => setEditingId(null)}
                            className="rounded-xl border border-slate-700 bg-slate-950 px-3 py-2 text-sm text-slate-100 transition hover:bg-slate-900"
                          >
                            Cancel
                          </button>
                        </>
                      ) : (
                        <>
                          <button
                            type="button"
                            onClick={() => beginEdit(sound)}
                            className="rounded-xl border border-slate-700 bg-slate-950 px-3 py-2 text-sm text-slate-100 transition hover:bg-slate-900"
                          >
                            Edit
                          </button>
                          <button
                            type="button"
                            onClick={() => void handleDelete(sound)}
                            disabled={deletingId === sound.id}
                            className="rounded-xl border border-rose-500/40 bg-rose-500/10 px-3 py-2 text-sm text-rose-100 transition hover:bg-rose-500/20 disabled:opacity-60"
                          >
                            {deletingId === sound.id ? "Deleting…" : "Delete"}
                          </button>
                        </>
                      )}
                    </div>
                  </div>
                </div>
              </div>
            );
          })}

          {!isLoading && sounds.length === 0 && (
            <div className="rounded-xl border border-dashed border-slate-800 bg-slate-950/30 p-4 text-sm text-slate-400">
              Upload your first clip to populate the guild soundboard.
            </div>
          )}
        </div>
      </SectionCard>

      <div className="flex justify-end">
        <button
          type="button"
          onClick={() => void refreshSounds()}
          className="rounded-xl border border-slate-700 bg-slate-950 px-3 py-2 text-sm text-slate-100 transition hover:bg-slate-900"
        >
          Refresh sounds
        </button>
      </div>
    </div>
  );
}

