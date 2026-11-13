import { useCallback, useEffect, useMemo, useState } from "react";
import { firestore, missingConfig } from "./firebase.js";
import { fetchSessions, fetchSessionRounds, SCENES } from "./firestore.js";

function errorMessage(error) {
  const code = error?.code || "";
  if (code.includes("permission-denied")) {
    return "Firestore denegó la lectura. Revisa las reglas de acceso de la base de datos.";
  }
  if (code.includes("network-request-failed")) {
    return "No se pudo conectar. Comprueba internet y vuelve a intentar.";
  }
  return error?.message || "Ocurrió un error inesperado.";
}

function dateLabel(timestamp) {
  const date = timestampDate(timestamp);
  return date && !Number.isNaN(date.getTime())
    ? date.toLocaleString("es-CL")
    : "Fecha no disponible";
}

function timestampDate(timestamp) {
  if (timestamp && typeof timestamp.toDate === "function") return timestamp.toDate();
  return timestamp ? new Date(timestamp) : null;
}

function originalSessionDate(session) {
  const date = timestampDate(session?.session_started_at);
  return date && !Number.isNaN(date.getTime()) ? date : null;
}

function uploadedSessionDate(session) {
  const date = timestampDate(session?.updated_at);
  return date && !Number.isNaN(date.getTime()) ? date : null;
}

function sessionDate(session) {
  const startedAt = originalSessionDate(session);
  if (startedAt) return startedAt;
  return uploadedSessionDate(session);
}

function sessionDateLabel(session, showUploadDate = false) {
  const startedAt = originalSessionDate(session);
  const uploadedAt = uploadedSessionDate(session);
  if (startedAt) {
    return showUploadDate
      ? `Sesión · ${dateLabel(startedAt)}`
      : dateLabel(startedAt);
  }
  return uploadedAt
    ? `Última carga · ${dateLabel(uploadedAt)}`
    : "Fecha no disponible";
}

function localDateKey(date) {
  return `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, "0")}-${String(date.getDate()).padStart(2, "0")}`;
}

function sessionDateGroupLabel(date, key) {
  if (!date) return "Fecha desconocida";
  const today = new Date();
  const yesterday = new Date(today.getFullYear(), today.getMonth(), today.getDate() - 1);
  if (key === localDateKey(today)) return "Hoy";
  if (key === localDateKey(yesterday)) return "Ayer";
  return date.toLocaleDateString("es-CL", {
    weekday: "long",
    year: "numeric",
    month: "long",
    day: "numeric",
  });
}

const SESSION_TAGS_STORAGE_KEY = "neuroflex.firebaseViewer.sessionTags.v1";
const SESSION_CUTOFF_STORAGE_KEY = "neuroflex.firebaseViewer.sessionCutoff.v1";

function loadSessionCutoff() {
  try {
    const stored = localStorage.getItem(SESSION_CUTOFF_STORAGE_KEY) || "";
    if (stored && Number.isNaN(new Date(stored).getTime())) {
      throw new Error("La fecha de corte guardada no es válida.");
    }
    return stored;
  } catch (error) {
    console.error("No se pudo cargar la fecha de corte local.", error);
    return "";
  }
}

function loadSessionTags() {
  try {
    const stored = localStorage.getItem(SESSION_TAGS_STORAGE_KEY);
    if (!stored) return {};
    const tags = JSON.parse(stored);
    if (!tags || typeof tags !== "object" || Array.isArray(tags)) {
      throw new Error("El formato de etiquetas guardado no es válido.");
    }
    return tags;
  } catch (error) {
    console.error("No se pudieron cargar las etiquetas locales de sesiones.", error);
    return {};
  }
}

function humanize(key) {
  const labels = {
    activeTimeMs: "Tiempo activo",
    aidsUsed: "Ayudas utilizadas",
    actualPigeonCount: "Palomas mostradas",
    answerSubmittedAt: "Respuesta enviada",
    answerEnabledAt: "Respuesta habilitada",
    assessmentMode: "Modo de evaluación",
    comparisonGroupId: "Grupo de comparación",
    correctPairAttempts: "Intentos correctos",
    correctPlacements: "Piezas correctas",
    deckId: "Mazo",
    displayDurationMs: "Tiempo de observación",
    distractionCount: "Cantidad de distracciones",
    distractionDurationMs: "Duración de distracciones",
    distractionIntensity: "Intensidad de distracciones",
    distractionType: "Tipo de distracción",
    distractionsEnabled: "Distracciones activadas",
    endedAt: "Finalizada",
    incorrectPairAttempts: "Intentos incorrectos",
    incorrectPlacements: "Colocaciones incorrectas",
    incorrectMoves: "Movimientos incorrectos",
    movementLevel: "Movimiento",
    pausedDurationMs: "Tiempo en pausa",
    pairsFound: "Parejas encontradas",
    pigeonsHiddenAt: "Palomas ocultadas",
    previewDurationMs: "Tiempo de vista previa",
    puzzleId: "Puzzle",
    responseTimeMs: "Tiempo de respuesta",
    retentionDelayMs: "Tiempo de retención",
    rotationEnabled: "Rotación activada",
    scenarioVersion: "Versión de escena",
    startedAt: "Iniciada",
    status: "Estado",
    totalCards: "Total de cartas",
    totalPairs: "Total de parejas",
    totalPieces: "Total de piezas",
    userPigeonCount: "Respuesta del usuario",
  };
  if (labels[key]) return labels[key];
  return key
    .replace(/([a-z0-9])([A-Z])/g, "$1 $2")
    .replace(/_/g, " ")
    .replace(/^./, (letter) => letter.toLocaleUpperCase("es-CL"));
}

const durationMetrics = new Set([
  "activeTimeMs",
  "displayDurationMs",
  "distractionDurationMs",
  "pausedDurationMs",
  "previewDurationMs",
  "responseTimeMs",
  "retentionDelayMs",
]);

const metricOrder = {
  pigeons: [
    "actualPigeonCount",
    "userPigeonCount",
    "responseTimeMs",
    "displayDurationMs",
    "pigeonsHiddenAt",
    "answerEnabledAt",
    "answerSubmittedAt",
    "retentionDelayMs",
    "movementLevel",
    "distractionsEnabled",
    "distractionType",
    "distractionCount",
    "distractionIntensity",
    "distractionDurationMs",
    "pausedDurationMs",
    "assessmentMode",
    "scenarioVersion",
    "startedAt",
    "endedAt",
  ],
  memory: [
    "pairsFound",
    "totalPairs",
    "correctPairAttempts",
    "incorrectPairAttempts",
    "totalCards",
    "activeTimeMs",
    "previewDurationMs",
    "aidsUsed",
    "deckId",
    "startedAt",
    "endedAt",
  ],
  puzzle: [
    "correctPlacements",
    "totalPieces",
    "incorrectPlacements",
    "incorrectMoves",
    "activeTimeMs",
    "aidsUsed",
    "puzzleId",
    "rotationEnabled",
    "startedAt",
    "endedAt",
  ],
};

const metricIcons = {
  activeTimeMs: "◷",
  aidsUsed: "✦",
  actualPigeonCount: "●",
  answerSubmittedAt: "◷",
  answerEnabledAt: "◷",
  correctPairAttempts: "✓",
  correctPlacements: "✓",
  displayDurationMs: "◷",
  distractionDurationMs: "◷",
  endedAt: "◷",
  incorrectPairAttempts: "↻",
  incorrectPlacements: "↻",
  incorrectMoves: "↻",
  pairsFound: "✓",
  pausedDurationMs: "◷",
  previewDurationMs: "◷",
  responseTimeMs: "◷",
  retentionDelayMs: "◷",
  startedAt: "◷",
  totalCards: "▤",
  totalPairs: "▤",
  totalPieces: "▤",
  userPigeonCount: "●",
};

const sceneIcons = {
  memory: "🃏",
  pigeons: "🕊️",
  puzzle: "🧩",
};

const technicalMetricKeys = new Set([
  "contractVersion",
  "difficultyLevel",
  "participantId",
  "roundId",
  "scenarioId",
  "sessionId",
  "status",
]);

function metricIcon(key) {
  return metricIcons[key] || "•";
}

function metricValue(key, value) {
  if (typeof value === "boolean") return value ? "Sí" : "No";
  if (durationMetrics.has(key) && typeof value === "number") {
    return `${(value / 1000).toLocaleString("es-CL", { maximumFractionDigits: 1 })} s`;
  }
  if (["answerEnabledAt", "answerSubmittedAt", "endedAt", "startedAt"].includes(key)) {
    const date = new Date(value);
    if (!Number.isNaN(date.getTime())) return date.toLocaleString("es-CL");
  }
  return String(value);
}

function isMissingMetric(value) {
  return value === null ||
    value === undefined ||
    (typeof value === "string" && value.trim() === "") ||
    (Array.isArray(value) && value.length === 0) ||
    (typeof value === "object" && !Array.isArray(value) && Object.keys(value).length === 0);
}

function metricEntries(value, prefix = "") {
  if (isMissingMetric(value)) {
    return prefix ? [[prefix, "Sin dato", true]] : [];
  }

  if (Array.isArray(value)) {
    return value.flatMap((item, index) =>
      metricEntries(item, `${prefix}[${index + 1}]`),
    );
  }

  if (typeof value === "object") {
    return Object.entries(value).flatMap(([key, item]) =>
      metricEntries(item, prefix ? `${prefix}.${key}` : key),
    );
  }

  const key = prefix.split(".").pop()?.replace(/\[\d+\]/g, "");
  return [[prefix, metricValue(key, value), false]];
}

function metricsForRound(round, sceneId) {
  let metrics = round.metrics;
  if (!metrics && round.metrics_json) {
    try {
      metrics = JSON.parse(round.metrics_json);
    } catch {
      return [["metrics_json", "El JSON de métricas no es válido."]];
    }
  }

  const pigeonRound = metrics?.request?.rounds?.[0];
  if (pigeonRound) {
    metrics = {
      ...pigeonRound,
      ...(metrics.arrivalMetadata
        ? { arrivalMetadata: metrics.arrivalMetadata }
        : {}),
    };
  }

  const order = new Map((metricOrder[sceneId] || []).map((key, index) => [key, index]));
  return metricEntries(metrics || {})
    .filter(([key]) => !technicalMetricKeys.has(key.split(".").pop()?.replace(/\[\d+\]/g, "")))
    .sort((left, right) => {
      const leftKey = left[0].split(".").pop()?.replace(/\[\d+\]/g, "");
      const rightKey = right[0].split(".").pop()?.replace(/\[\d+\]/g, "");
      const leftRank = order.has(leftKey) ? order.get(leftKey) : Number.MAX_SAFE_INTEGER;
      const rightRank = order.has(rightKey) ? order.get(rightKey) : Number.MAX_SAFE_INTEGER;
      return leftRank - rightRank || humanize(leftKey).localeCompare(humanize(rightKey), "es");
    });
}

function SetupNotice() {
  return (
    <main className="setup-shell">
      <section className="setup-card">
        <div className="brand-mark">NF</div>
        <p className="eyebrow">VISOR DE PRUEBAS FIREBASE</p>
        <h1>Configura la conexión</h1>
        <p>
          Este visor usa una app web registrada en el proyecto Firebase y sólo
          consulta Firestore. No guardes contraseñas ni claves de cuenta de
          servicio en esta aplicación. Unity también necesita permiso para
          reservar códigos de sesión en Firestore.
        </p>
        <ol>
          <li>
            En Firebase Console, registra una aplicación <strong>Web</strong> en
            el proyecto <code>newroflex</code>.
          </li>
          <li>
            Copia sus valores de configuración a <code>.env.local</code>, usando
            <code> .env.example</code> como plantilla.
          </li>
          <li>
            Verifica que las reglas de Firestore permitan lectura al visor y
            lectura/escritura de <code>metadata/sessionCodeCounter</code> a Unity.
          </li>
        </ol>
        {missingConfig.length > 0 && (
          <p className="missing-config">
            Faltan estas variables: {missingConfig.join(", ")}
          </p>
        )}
        <p className="muted">
          Después de crear o cambiar <code>.env.local</code>, reinicia la app.
        </p>
      </section>
    </main>
  );
}

function SessionRow({ session, displayCode, tag, selected, onSelect, showUploadDate }) {
  const isTestSession = session.id.startsWith("s-test-");

  return (
    <button
      aria-label={`Seleccionar ${displayCode}`}
      className={`session-row${selected ? " selected" : ""}`}
      onClick={onSelect}
      type="button"
    >
      <span className="session-icon">S</span>
      <span className="session-copy">
        <strong title={displayCode}>{displayCode}</strong>
        <small>{sessionDateLabel(session, showUploadDate)}</small>
        {tag ? (
          <span className="session-tag" style={{ "--tag-color": tag.color }}>{tag.name}</span>
        ) : isTestSession ? (
          <span className="session-tag default-test-tag">Prueba</span>
        ) : null}
      </span>
    </button>
  );
}

function RoundCard({ round, sceneId, showMissing }) {
  const metrics = useMemo(() => metricsForRound(round, sceneId), [round, sceneId]);
  const status = round.status || "sin estado";
  const statusLabel = {
    abandoned: "Abandonada",
    completed: "Completada",
    interrupted: "Interrumpida",
  }[status.toLowerCase()] || status;
  const visibleMetrics = metrics.filter(([, , missing]) => showMissing || !missing);

  return (
    <article className="round-card">
      <header className="round-heading">
        <div className="round-result">
          <span className={`status-dot ${status.toLowerCase()}`} />
          <strong>Métricas de la ronda</strong>
          <span className="round-status">{statusLabel}</span>
        </div>
        <span className="round-date">{dateLabel(round.ended_at)}</span>
      </header>
      {visibleMetrics.length > 0 ? (
        <dl className="metric-list">
          {visibleMetrics.map(([key, value, missing]) => {
            const name = key.split(".").pop()?.replace(/\[\d+\]/g, "");
            return (
              <div className={`metric${missing ? " missing" : ""}`} key={key}>
                <dt>
                  <span className="metric-icon" aria-hidden="true">{metricIcon(name)}</span>
                  {humanize(name)}
                </dt>
                <dd title={value}>{value}</dd>
              </div>
            );
          })}
        </dl>
      ) : (
        <p className="empty-inline metric-empty">
          {metrics.length > 0
            ? "Los campos vacíos están ocultos."
            : "No hay métricas disponibles para esta ronda."}
        </p>
      )}
    </article>
  );
}

function SceneSection({ scene, index, showMissing }) {
  const sceneId = scene.scenario_id || scene.id;
  const sceneName = scene.scenario_name || SCENES[sceneId] || sceneId;
  const roundCount = scene.difficulties.reduce(
    (total, difficulty) => total + difficulty.rounds.length,
    0,
  );
  const completedDifficulties = scene.difficulties.filter(
    (difficulty) =>
      difficulty.rounds.length > 0 &&
      difficulty.rounds.every((round) => round.status?.toLowerCase() === "completed"),
  ).length;

  return (
    <details className={`scene-section scene-theme-${sceneIcons[sceneId] ? sceneId : "default"}`}>
      <summary className="scene-heading scene-toggle">
        <span className="scene-index">{String(index).padStart(2, "0")}</span>
        <div className="scene-title">
          <span className="scene-icon" aria-hidden="true">{sceneIcons[sceneId] || "◉"}</span>
          <div>
            <h3>{sceneName}</h3>
            <p className="scene-progress">
              {completedDifficulties} de {scene.difficulties.length} dificultades completadas
            </p>
          </div>
        </div>
        <span className="count-pill">
          {roundCount} {roundCount === 1 ? "ronda" : "rondas"}
        </span>
      </summary>
      <div className="scene-content">
        {scene.difficulties.map((difficulty) => {
          const completedRounds = difficulty.rounds.filter(
            (round) => round.status?.toLowerCase() === "completed",
          ).length;
          const isComplete = difficulty.rounds.length > 0 &&
            completedRounds === difficulty.rounds.length;

          return (
            <details
              className={`difficulty-group difficulty-color-${Number(difficulty.difficulty || difficulty.id)}`}
              key={difficulty.id}
            >
              <summary className="difficulty-heading">
                <span className="difficulty-marker" aria-hidden="true">
                  {difficulty.difficulty || difficulty.id}
                </span>
                <span className="difficulty-copy">
                  <strong>{difficulty.difficulty_name || `Dificultad ${difficulty.difficulty || difficulty.id}`}</strong>
                  <small>{completedRounds}/{difficulty.rounds.length} rondas completadas</small>
                </span>
                <span className={`difficulty-status${isComplete ? " complete" : ""}`}>
                  {isComplete
                    ? "Completa"
                    : difficulty.rounds.length > 0
                      ? "Incompleta"
                      : "Sin rondas"}
                </span>
              </summary>
              <div className="difficulty-content">
                {difficulty.rounds.length === 0 ? (
                  <p className="empty-inline">Sin rondas guardadas.</p>
                ) : (
                  <div className="round-list">
                    {difficulty.rounds.map((round) => (
                      <RoundCard key={round.id} round={round} sceneId={sceneId} showMissing={showMissing} />
                    ))}
                  </div>
                )}
              </div>
            </details>
          );
        })}
      </div>
    </details>
  );
}

export default function App() {
  const [sessions, setSessions] = useState([]);
  const [selectedId, setSelectedId] = useState("");
  const [sessionData, setSessionData] = useState(null);
  const [loadingSessions, setLoadingSessions] = useState(false);
  const [loadingDetails, setLoadingDetails] = useState(false);
  const [sessionError, setSessionError] = useState("");
  const [detailError, setDetailError] = useState("");
  const [search, setSearch] = useState("");
  const [lastRefresh, setLastRefresh] = useState(null);
  const [detailRefreshKey, setDetailRefreshKey] = useState(0);
  const [sessionTags, setSessionTags] = useState(loadSessionTags);
  const [cutoffValue, setCutoffValue] = useState(loadSessionCutoff);
  const [cutoffError, setCutoffError] = useState("");
  const [tagEditorOpen, setTagEditorOpen] = useState(false);
  const [tagNameDraft, setTagNameDraft] = useState("");
  const [tagColorDraft, setTagColorDraft] = useState("#a99cf2");
  const [tagError, setTagError] = useState("");
  const [showMissing, setShowMissing] = useState(false);

  const refreshSessions = useCallback(async () => {
    if (!firestore) return;
    setLoadingSessions(true);
    setSessionError("");
    try {
      const result = await fetchSessions(firestore);
      setSessions(result);
      setLastRefresh(new Date());
      setSelectedId((current) =>
        result.some((session) => session.id === current)
          ? current
          : result[0]?.id || "",
      );
    } catch (error) {
      setSessionError(errorMessage(error));
    } finally {
      setLoadingSessions(false);
    }
  }, []);

  useEffect(() => {
    refreshSessions();
  }, [refreshSessions]);

  useEffect(() => {
    setTagEditorOpen(false);
    setTagError("");
  }, [selectedId]);

  useEffect(() => {
    if (!selectedId || !firestore) {
      setSessionData(null);
      return undefined;
    }

    let active = true;
    setLoadingDetails(true);
    setDetailError("");
    fetchSessionRounds(firestore, selectedId)
      .then((data) => {
        if (active) setSessionData(data);
      })
      .catch((error) => {
        if (active) setDetailError(errorMessage(error));
      })
      .finally(() => {
        if (active) setLoadingDetails(false);
      });

    return () => {
      active = false;
    };
  }, [selectedId, detailRefreshKey]);

  const filteredSessions = useMemo(() => {
    const term = search.trim().toLocaleLowerCase("es-CL");
    if (!term) return sessions;
    return sessions.filter((session) => {
      const tag = sessionTags[session.id];
      const values = [
        session.session_code,
        session.id.startsWith("s-test-") ? "prueba anterior" : "sesión anterior",
        tag?.name,
        sessionDateLabel(session, Boolean(cutoffValue)),
        session.id.startsWith("s-test-") ? "prueba" : "",
      ];
      return values.some((value) =>
        value?.toLocaleLowerCase("es-CL").includes(term),
      );
    });
  }, [sessions, search, sessionTags, cutoffValue]);

  const legacySessionLabels = useMemo(() => {
    let historicalIndex = 0;
    return new Map(
      sessions
        .filter((session) => !session.session_code)
        .map((session) => {
          historicalIndex++;
          const label = session.id.startsWith("s-test-")
            ? `Prueba anterior ${String(historicalIndex).padStart(3, "0")}`
            : `Sesión anterior ${String(historicalIndex).padStart(3, "0")}`;
          return [session.id, label];
        }),
    );
  }, [sessions]);

  const sessionGroups = useMemo(() => {
    const cutoffDate = cutoffValue ? new Date(cutoffValue) : null;
    const hasCutoff = cutoffDate && !Number.isNaN(cutoffDate.getTime());
    const groups = new Map();
    for (const session of filteredSessions) {
      const date = originalSessionDate(session) ||
        (hasCutoff ? uploadedSessionDate(session) : null);
      const dateKey = date ? localDateKey(date) : "";
      const period = hasCutoff && date
        ? date.getTime() >= cutoffDate.getTime() ? "after" : "before"
        : null;
      const key = hasCutoff
        ? date ? `${period}:${dateKey}` : "unknown"
        : date ? dateKey : "historical";
      if (!groups.has(key)) {
        groups.set(key, {
          key,
          period,
          label: hasCutoff && date
            ? `${period === "before" ? "Historial anterior al corte" : "Pruebas desde el corte"} · ${sessionDateGroupLabel(date, dateKey)}`
            : hasCutoff
              ? "Sin fecha para clasificar"
              : date
                ? sessionDateGroupLabel(date, dateKey)
                : "Historial anterior · fecha original no registrada",
          sessions: [],
        });
      }
      groups.get(key).sessions.push(session);
    }
    return Array.from(groups.values())
      .map((group) => ({
        ...group,
        sessions: group.sessions.sort((left, right) =>
          (sessionDate(right)?.getTime() || 0) - (sessionDate(left)?.getTime() || 0),
        ),
      }))
      .sort((left, right) => {
        if (left.key === right.key) return 0;
        if (hasCutoff && left.period !== right.period) {
          const periodOrder = { before: 0, after: 1, null: 2 };
          return periodOrder[left.period] - periodOrder[right.period];
        }
        if (hasCutoff && left.period === null) return 1;
        if (hasCutoff && right.period === null) return -1;
        if (left.key === "historical") return 1;
        if (right.key === "historical") return -1;
        return right.key.localeCompare(left.key);
      });
  }, [filteredSessions, cutoffValue]);

  const selectedSession = sessions.find((session) => session.id === selectedId);
  const selectedDisplayCode = selectedSession?.session_code ||
    legacySessionLabels.get(selectedId) ||
    "Sesión";
  const selectedTag = sessionTags[selectedId];
  const scenes = sessionData?.scenes || [];
  const totalRounds = scenes.reduce(
    (total, scene) =>
      total + scene.difficulties.reduce((sum, difficulty) => sum + difficulty.rounds.length, 0),
    0,
  ) + (sessionData?.legacyRounds.length || 0);
  const completedRounds = scenes.reduce(
    (total, scene) =>
      total + scene.difficulties.reduce(
        (sum, difficulty) =>
          sum + difficulty.rounds.filter((round) => round.status?.toLowerCase() === "completed").length,
        0,
      ),
    0,
  ) + (sessionData?.legacyRounds.filter((round) => round.status?.toLowerCase() === "completed").length || 0);
  const totalDifficulties = scenes.reduce((total, scene) => total + scene.difficulties.length, 0);
  const completedDifficulties = scenes.reduce(
    (total, scene) =>
      total + scene.difficulties.filter(
        (difficulty) =>
          difficulty.rounds.length > 0 &&
          difficulty.rounds.every((round) => round.status?.toLowerCase() === "completed"),
      ).length,
    0,
  );
  const rounds = [
    ...scenes.flatMap((scene) =>
      scene.difficulties.flatMap((difficulty) => difficulty.rounds),
    ),
    ...(sessionData?.legacyRounds || []),
  ];
  const hiddenMetricCount = rounds.reduce(
    (total, round) =>
      total + metricsForRound(round, round.scenario_id || "").filter(([, , missing]) => missing).length,
    0,
  );
  const isTestSession = selectedSession?.id.startsWith("s-test-") || false;

  function persistSessionTags(nextTags) {
    try {
      localStorage.setItem(SESSION_TAGS_STORAGE_KEY, JSON.stringify(nextTags));
      setSessionTags(nextTags);
      setTagError("");
      return true;
    } catch (error) {
      console.error("No se pudieron guardar las etiquetas locales de sesiones.", error);
      setTagError("No se pudo guardar la etiqueta en este equipo.");
      return false;
    }
  }

  function openTagEditor() {
    setTagNameDraft(selectedTag?.name || "");
    setTagColorDraft(selectedTag?.color || "#a99cf2");
    setTagError("");
    setTagEditorOpen(true);
  }

  function saveSessionTag(event) {
    event.preventDefault();
    const name = tagNameDraft.trim();
    if (!name) {
      setTagError("Escribe un nombre para la etiqueta.");
      return;
    }
    if (!/^#[0-9a-f]{6}$/i.test(tagColorDraft)) {
      setTagError("El color de la etiqueta no es válido.");
      return;
    }
    if (persistSessionTags({
      ...sessionTags,
      [selectedId]: { name, color: tagColorDraft },
    })) {
      setTagEditorOpen(false);
    }
  }

  function removeSessionTag() {
    const nextTags = { ...sessionTags };
    delete nextTags[selectedId];
    if (persistSessionTags(nextTags)) setTagEditorOpen(false);
  }

  function updateCutoff(value) {
    setCutoffValue(value);
    try {
      if (value) {
        localStorage.setItem(SESSION_CUTOFF_STORAGE_KEY, value);
      } else {
        localStorage.removeItem(SESSION_CUTOFF_STORAGE_KEY);
      }
      setCutoffError("");
    } catch (error) {
      console.error("No se pudo guardar la fecha de corte local.", error);
      setCutoffError("No se pudo guardar la fecha de corte en este equipo.");
    }
  }

  if (missingConfig.length > 0) return <SetupNotice />;

  return (
    <div className="app-shell">
      <header className="topbar">
        <div className="brand">
          <div className="brand-mark small">NF</div>
          <div>
            <strong>NeuroFlex</strong>
            <span>Visor Firebase</span>
          </div>
        </div>
        <div className="topbar-actions">
          <span className="connected"><span />{import.meta.env.VITE_FIREBASE_PROJECT_ID}</span>
        </div>
      </header>

      <main className="workspace">
        <aside className="session-panel">
          <div className="panel-title">
            <div>
              <p className="eyebrow">FIRESTORE</p>
              <h1>Sesiones</h1>
            </div>
            <button
              aria-label="Actualizar sesiones"
              className="icon-button"
              disabled={loadingSessions}
              onClick={refreshSessions}
              title="Actualizar sesiones (máximo 50)"
              type="button"
            >
              {loadingSessions ? <span className="spinner small-spinner" /> : "↻"}
            </button>
          </div>
          <label className="search-box">
            <span>⌕</span>
            <input
              placeholder="Buscar código, tag o fecha..."
              value={search}
              onChange={(event) => setSearch(event.target.value)}
            />
          </label>
          <div className="cutoff-control">
            <label htmlFor="session-cutoff">Fecha y hora de corte</label>
            <div className="cutoff-input-row">
              <input
                id="session-cutoff"
                type="datetime-local"
                step="60"
                value={cutoffValue}
                onChange={(event) => updateCutoff(event.target.value)}
              />
              {cutoffValue && (
                <button
                  aria-label="Quitar fecha de corte"
                  className="cutoff-clear"
                  onClick={() => updateCutoff("")}
                  title="Quitar fecha de corte"
                  type="button"
                >
                  ×
                </button>
              )}
            </div>
            <p className="cutoff-help">
              {cutoffValue
                ? "Separa por la fecha original de la sesión; si falta, usa la última carga. Hora local."
                : "Opcional: elige cuándo comienzan las pruebas para separarlas del historial."}
            </p>
            {cutoffError && <p className="error-banner cutoff-error">{cutoffError}</p>}
          </div>
          <p className="list-hint">
            {filteredSessions.length} de {sessions.length} sesiones · {cutoffValue ? "separadas por corte" : "agrupadas por fecha"}
          </p>
          {sessionError && <p className="error-banner compact">{sessionError}</p>}
          <div className="session-list">
            {sessionGroups.map((group) => (
              <details
                className={`session-date-group${group.period ? ` cutoff-${group.period}` : ""}${cutoffValue && group.period === null ? " cutoff-unknown" : ""}`}
                key={group.key}
                open
              >
                <summary>
                  <span>{group.label}</span>
                  <small>{group.sessions.length}</small>
                </summary>
                <div className="session-group-list">
                  {group.sessions.map((session) => (
                    <SessionRow
                      key={session.id}
                      session={session}
                      displayCode={session.session_code || legacySessionLabels.get(session.id)}
                      tag={sessionTags[session.id]}
                      selected={session.id === selectedId}
                      showUploadDate={Boolean(cutoffValue)}
                      onSelect={() => setSelectedId(session.id)}
                    />
                  ))}
                </div>
              </details>
            ))}
            {!loadingSessions && sessionGroups.length === 0 && (
              <p className="empty-state">
                {search ? "No hay sesiones que coincidan." : "No hay sesiones cargadas."}
              </p>
            )}
          </div>
          <p className="refresh-time">
            {lastRefresh
              ? `Actualizado ${lastRefresh.toLocaleTimeString("es-CL")}`
              : "Pulsa actualizar para leer Firestore."}
          </p>
        </aside>

        <section className="details-panel">
          {!selectedId ? (
            <div className="empty-main">
              <div className="empty-icon">▤</div>
              <h2>Selecciona una sesión</h2>
              <p>Las escenas, dificultades y métricas se cargarán al seleccionarla.</p>
            </div>
          ) : (
            <>
              <header className="detail-header">
                <div>
                  <p className="eyebrow">SESIÓN</p>
                  <h2>{selectedDisplayCode}</h2>
                  <p className="muted">
                    {isTestSession ? "Sesión de prueba" : selectedSession?.anonymous === false ? "Perfil" : "Anónima"}
                    {" · "}
                    {originalSessionDate(selectedSession)
                      ? `Fecha de sesión: ${dateLabel(sessionDate(selectedSession))}`
                      : `Fecha original no registrada · ${sessionDateLabel(selectedSession)}`}
                  </p>
                  {selectedTag && (
                    <span className="session-tag detail-tag" style={{ "--tag-color": selectedTag.color }}>
                      {selectedTag.name}
                    </span>
                  )}
                </div>
                <div className="detail-actions">
                  <button className="quiet-button" onClick={openTagEditor} type="button">
                    {selectedTag ? "Editar tag" : "Añadir tag"}
                  </button>
                  <button
                    className="primary-button"
                    disabled={loadingDetails}
                    onClick={() => setDetailRefreshKey((key) => key + 1)}
                    type="button"
                  >
                    {loadingDetails ? "Actualizando..." : "Actualizar datos"}
                  </button>
                </div>
              </header>
              {tagEditorOpen && (
                <form className="tag-editor" onSubmit={saveSessionTag}>
                  <label>
                    Nombre del tag
                    <input
                      autoFocus
                      maxLength={24}
                      value={tagNameDraft}
                      onChange={(event) => setTagNameDraft(event.target.value)}
                      placeholder="Ej.: Prueba de hoy"
                      required
                    />
                  </label>
                  <label className="tag-color-field">
                    Color
                    <input
                      aria-label="Color de etiqueta"
                      type="color"
                      value={tagColorDraft}
                      onChange={(event) => setTagColorDraft(event.target.value)}
                    />
                  </label>
                  <p className="tag-help">El tag se guarda solo en este equipo.</p>
                  <button className="primary-button" type="submit">Guardar tag</button>
                  {selectedTag && (
                    <button className="quiet-button" onClick={removeSessionTag} type="button">
                      Quitar tag
                    </button>
                  )}
                  <button className="quiet-button" onClick={() => setTagEditorOpen(false)} type="button">
                    Cancelar
                  </button>
                  {tagError && <p className="error-banner tag-error">{tagError}</p>}
                </form>
              )}
              {detailError && <p className="error-banner">{detailError}</p>}
              <div className="summary-strip">
                <div><strong>{completedRounds}/{totalRounds}</strong><span>Rondas completadas</span></div>
                <div><strong>{scenes.length}</strong><span>Escenas</span></div>
                <div><strong>{completedDifficulties}/{totalDifficulties}</strong><span>Dificultades completadas</span></div>
                <small>Agrupación por sesión, escena y dificultad</small>
              </div>
              <div className="metric-visibility">
                <p>Los datos vacíos se ocultan para facilitar la lectura.</p>
                <button
                  className="quiet-button"
                  disabled={hiddenMetricCount === 0}
                  onClick={() => setShowMissing((visible) => !visible)}
                  type="button"
                >
                  {showMissing
                    ? "Ocultar datos vacíos"
                    : `Mostrar datos vacíos${hiddenMetricCount ? ` (${hiddenMetricCount})` : ""}`}
                </button>
              </div>
              {loadingDetails ? (
                <div className="center-state inline"><span className="spinner" />Leyendo escenas y métricas...</div>
              ) : scenes.length === 0 && !sessionData?.legacyRounds.length ? (
                <div className="empty-main">
                  <h2>Esta sesión no tiene rondas</h2>
                  <p>Revisa que la app haya guardado datos en la estructura actual de Firestore.</p>
                </div>
              ) : (
                <>
                  {scenes.length > 0 && (
                    <div className="scene-list-heading">
                      <h3>Escenas de la sesión</h3>
                      <span>Selecciona una escena para ver sus dificultades y métricas.</span>
                    </div>
                  )}
                  <div className="scene-list">
                    {scenes.map((scene, index) => (
                      <SceneSection
                        key={scene.id}
                        index={index + 1}
                        scene={scene}
                        showMissing={showMissing}
                      />
                    ))}
                    {sessionData?.legacyRounds.length > 0 && (
                      <section className="scene-section">
                        <header className="scene-heading">
                          <div className="scene-title">
                            <span className="scene-icon" aria-hidden="true">📋</span>
                            <div><h3>Rondas sin clasificar</h3><p className="scene-progress">Estructura anterior</p></div>
                          </div>
                          <span className="count-pill">{sessionData.legacyRounds.length} rondas</span>
                        </header>
                        <div className="difficulty-content">
                          <div className="round-list">
                            {sessionData.legacyRounds.map((round) => (
                              <RoundCard
                                key={round.id}
                                round={round}
                                sceneId={round.scenario_id || ""}
                                showMissing={showMissing}
                              />
                            ))}
                          </div>
                        </div>
                      </section>
                    )}
                  </div>
                </>
              )}
            </>
          )}
        </section>
      </main>
    </div>
  );
}
