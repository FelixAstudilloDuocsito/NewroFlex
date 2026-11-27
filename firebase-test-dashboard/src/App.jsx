import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { firestore, missingConfig } from "./firebase.js";
import {
  archiveSession as saveSessionArchive,
  deleteRound as removeRoundFromFirestore,
  deleteSession as removeSessionFromFirestore,
  fetchSessionRounds,
  fetchSessionSceneCounts,
  fetchSessions,
  removeSessionTag as deleteSessionTag,
  restoreSession as clearSessionArchive,
  saveSessionTag as writeSessionTag,
  SCENES,
} from "./firestore.js";

function errorMessage(error) {
  const code = error?.code || "";
  if (code.includes("permission-denied")) {
    return "Firestore denegó el acceso. Revisa las reglas de lectura y escritura de la base de datos.";
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

const SESSION_CUTOFF_STORAGE_KEY = "neuroflex.firebaseViewer.sessionCutoff.v1";
const PAGE_SIZE_OPTIONS = [25, 50, 100];

function sessionSceneBadge(sceneCount) {
  if (typeof sceneCount !== "number") {
    return { className: "scene-count-loading", text: "·", title: "Consultando escenas..." };
  }
  if (sceneCount <= 0) {
    return { className: "scene-count-0", text: "0", title: "Sin escenas registradas" };
  }
  if (sceneCount === 1) {
    return { className: "scene-count-1", text: "1", title: "1 escena" };
  }
  if (sceneCount === 2) {
    return { className: "scene-count-2", text: "2", title: "2 escenas" };
  }
  return { className: "scene-count-3", text: String(sceneCount), title: `${sceneCount} escenas` };
}

function groupAndOrderSessions(sessionList, cutoffValue) {
  const cutoffDate = cutoffValue ? new Date(cutoffValue) : null;
  const hasCutoff = cutoffDate && !Number.isNaN(cutoffDate.getTime());
  const groups = new Map();

  for (const session of sessionList) {
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

  const sortedGroups = Array.from(groups.values())
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

  return {
    groups: sortedGroups,
    orderedSessions: sortedGroups.flatMap((group) => group.sessions),
  };
}

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
          Este visor usa una app web registrada en el proyecto Firebase para
          consultar sesiones y guardar sus tags en Firestore. No guardes
          contraseñas ni claves de cuenta de servicio en esta aplicación.
          Unity también necesita permiso para reservar códigos de sesión.
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
            Verifica que las reglas de Firestore permitan leer sesiones y
            actualizar sus tags, además de leer/escribir{" "}
            <code>metadata/sessionCodeCounter</code> a Unity.
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

function SessionRow({ session, displayCode, tag, sceneCount, selected, onSelect, showUploadDate }) {
  const isTestSession = session.id.startsWith("s-test-");
  const badge = sessionSceneBadge(sceneCount);

  return (
    <button
      aria-label={`Seleccionar ${displayCode}`}
      className={`session-row${selected ? " selected" : ""}`}
      onClick={onSelect}
      type="button"
    >
      <span
        aria-label={badge.title}
        className={`session-icon ${badge.className}`}
        title={badge.title}
      >
        {badge.text}
      </span>
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

function RoundCard({ round, sceneId, showMissing, onDelete }) {
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
        <div className="round-actions">
          <span className="round-date">{dateLabel(round.ended_at)}</span>
          {onDelete && (
            <button
              aria-label="Eliminar ronda de métricas"
              className="quiet-button round-delete-button"
              onClick={() => onDelete(round, statusLabel)}
              title="Eliminar esta entrada de métricas"
              type="button"
            >
              Eliminar
            </button>
          )}
        </div>
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

function SceneSection({ scene, index, showMissing, onRequestDeleteRound }) {
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
                      <RoundCard
                        key={round.id}
                        round={round}
                        sceneId={sceneId}
                        showMissing={showMissing}
                        onDelete={(targetRound, statusLabel) =>
                          onRequestDeleteRound({
                            sceneId: scene.id,
                            sceneName,
                            difficultyId: difficulty.id,
                            difficultyLabel: difficulty.difficulty_name || `Dificultad ${difficulty.difficulty || difficulty.id}`,
                            round: targetRound,
                            statusLabel,
                          })
                        }
                      />
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

function DeleteConfirmModal({
  target,
  step,
  confirmText,
  deleting,
  error,
  onConfirmTextChange,
  onStepChange,
  onCancel,
  onConfirm,
}) {
  if (!target) return null;

  const isSession = target.type === "session";
  const canSubmit = confirmText.trim().toUpperCase() === "ELIMINAR" && !deleting;

  function handleFormSubmit(event) {
    event.preventDefault();
    if (step === 3 && canSubmit) {
      onConfirm();
    }
  }

  return (
    <div
      className="modal-backdrop"
      onClick={(event) => {
        if (event.target === event.currentTarget && !deleting) onCancel();
      }}
      role="presentation"
    >
      <div
        aria-labelledby="delete-modal-title"
        aria-modal="true"
        className="confirm-modal"
        role="dialog"
      >
        <span className="confirm-step-pill">Confirmación {step} de 3</span>
        <h3 id="delete-modal-title">
          {step === 1 && (isSession ? "¿Eliminar esta sesión?" : "¿Eliminar esta entrada de métricas?")}
          {step === 2 && "¿Estás completamente seguro?"}
          {step === 3 && "Confirmación final"}
        </h3>

        <div className="confirm-target-box">
          {isSession ? (
            <>
              <strong>{target.displayCode}</strong>
              <span>
                {target.sceneCount} {target.sceneCount === 1 ? "escena" : "escenas"} ·{" "}
                {target.roundCount} {target.roundCount === 1 ? "ronda" : "rondas"}
              </span>
            </>
          ) : (
            <>
              <strong>{target.sceneName} · {target.difficultyLabel}</strong>
              <span>
                Estado: {target.statusLabel} · {target.roundDateLabel}
              </span>
            </>
          )}
        </div>

        {step === 1 && (
          <>
            <p className="muted">
              {isSession
                ? "Esta acción borrará la sesión junto con todas sus escenas, dificultades y rondas de métricas en Firebase."
                : "Esta acción borrará esta ronda y sus métricas de Firebase."}
            </p>
            {isSession && (
              <p className="confirm-warning">
                Si solo quieres ocultarla de la lista principal sin perder datos, cancela y usa{" "}
                <strong>Archivar sesión</strong>.
              </p>
            )}
            <div className="confirm-actions">
              <button className="quiet-button" onClick={onCancel} type="button">
                Cancelar
              </button>
              <button className="danger-button" onClick={() => onStepChange(2)} type="button">
                Continuar
              </button>
            </div>
          </>
        )}

        {step === 2 && (
          <>
            <p className="confirm-warning">
              {isSession
                ? "Se eliminarán permanentemente todos los documentos de esta sesión en Firestore. No se puede deshacer."
                : "Esta ronda de métricas se eliminará permanentemente de Firestore y no se podrá recuperar."}
            </p>
            <div className="confirm-actions">
              <button className="quiet-button" onClick={onCancel} type="button">
                Cancelar
              </button>
              <button className="quiet-button" onClick={() => onStepChange(1)} type="button">
                Volver
              </button>
              <button className="danger-button" onClick={() => onStepChange(3)} type="button">
                Sí, estoy seguro
              </button>
            </div>
          </>
        )}

        {step === 3 && (
          <form onSubmit={handleFormSubmit}>
            <p className="muted">
              Para evitar eliminaciones por accidente, escribe <code>ELIMINAR</code> y luego confirma.
            </p>
            <label className="confirm-input-label">
              Escribe ELIMINAR
              <input
                autoFocus
                disabled={deleting}
                onChange={(event) => onConfirmTextChange(event.target.value)}
                placeholder="ELIMINAR"
                value={confirmText}
              />
            </label>
            {error && <p className="error-banner">{error}</p>}
            <div className="confirm-actions">
              <button className="quiet-button" disabled={deleting} onClick={onCancel} type="button">
                Cancelar
              </button>
              <button className="quiet-button" disabled={deleting} onClick={() => onStepChange(2)} type="button">
                Volver
              </button>
              <button className="danger-button solid" disabled={!canSubmit} type="submit">
                {deleting ? "Eliminando..." : "Eliminar definitivamente"}
              </button>
            </div>
          </form>
        )}
      </div>
    </div>
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
  const [archiveError, setArchiveError] = useState("");
  const [search, setSearch] = useState("");
  const [showArchived, setShowArchived] = useState(false);
  const [savingArchive, setSavingArchive] = useState(false);
  const [lastRefresh, setLastRefresh] = useState(null);
  const [detailRefreshKey, setDetailRefreshKey] = useState(0);
  const [cutoffValue, setCutoffValue] = useState(loadSessionCutoff);
  const [cutoffError, setCutoffError] = useState("");
  const [tagEditorOpen, setTagEditorOpen] = useState(false);
  const [tagNameDraft, setTagNameDraft] = useState("");
  const [tagColorDraft, setTagColorDraft] = useState("#a99cf2");
  const [tagError, setTagError] = useState("");
  const [savingTag, setSavingTag] = useState(false);
  const [showMissing, setShowMissing] = useState(false);
  const [pageSize, setPageSize] = useState(50);
  const [currentPage, setCurrentPage] = useState(1);
  const [sceneCountsById, setSceneCountsById] = useState({});
  const sceneCountsRef = useRef({});
  const inFlightSceneCountsRef = useRef(new Set());
  const [deleteTarget, setDeleteTarget] = useState(null);
  const [deleteStep, setDeleteStep] = useState(1);
  const [deleteConfirmText, setDeleteConfirmText] = useState("");
  const [deletingEntry, setDeletingEntry] = useState(false);
  const [deleteError, setDeleteError] = useState("");

  const refreshSessions = useCallback(async () => {
    if (!firestore) return;
    setLoadingSessions(true);
    setSessionError("");
    try {
      const result = await fetchSessions(firestore);
      sceneCountsRef.current = {};
      inFlightSceneCountsRef.current.clear();
      setSceneCountsById({});
      setSessions(result);
      setLastRefresh(new Date());
      setSelectedId((current) =>
        result.some((session) => session.id === current)
          ? current
          : result.find((session) => session.archived !== true)?.id ||
            result[0]?.id ||
            "",
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
    setArchiveError("");
  }, [selectedId]);

  useEffect(() => {
    if (!deleteTarget) return undefined;
    function onKeyDown(event) {
      if (event.key === "Escape" && !deletingEntry) {
        setDeleteTarget(null);
      }
    }
    window.addEventListener("keydown", onKeyDown);
    return () => window.removeEventListener("keydown", onKeyDown);
  }, [deleteTarget, deletingEntry]);

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
        if (!active) return;
        setSessionData(data);
        let count = data.scenes.length;
        if (count === 0 && data.legacyRounds.length > 0) {
          const legacyScenes = new Set(
            data.legacyRounds.map((round) => round.scenario_id).filter(Boolean),
          );
          count = legacyScenes.size || 1;
        }
        sceneCountsRef.current[selectedId] = count;
        setSceneCountsById((prev) =>
          prev[selectedId] === count ? prev : { ...prev, [selectedId]: count },
        );
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

  const visibleSessions = useMemo(
    () => sessions.filter((session) => (session.archived === true) === showArchived),
    [sessions, showArchived],
  );

  const filteredSessions = useMemo(() => {
    const term = search.trim().toLocaleLowerCase("es-CL");
    if (!term) return visibleSessions;
    return visibleSessions.filter((session) => {
      const tag = session.tag;
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
  }, [visibleSessions, search, cutoffValue]);

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

  const { orderedSessions } = useMemo(
    () => groupAndOrderSessions(filteredSessions, cutoffValue),
    [filteredSessions, cutoffValue],
  );
  const totalPages = Math.max(1, Math.ceil(orderedSessions.length / pageSize));
  const safePage = Math.min(currentPage, totalPages);
  const pageStartIndex = (safePage - 1) * pageSize;

  const pagedSessions = useMemo(
    () => orderedSessions.slice(pageStartIndex, pageStartIndex + pageSize),
    [orderedSessions, pageStartIndex, pageSize],
  );

  const { groups: sessionGroups } = useMemo(
    () => groupAndOrderSessions(pagedSessions, cutoffValue),
    [pagedSessions, cutoffValue],
  );

  useEffect(() => {
    if (currentPage > totalPages) {
      setCurrentPage(totalPages);
    }
  }, [currentPage, totalPages]);

  useEffect(() => {
    if (!firestore || pagedSessions.length === 0) return undefined;
    const missingIds = pagedSessions
      .map((session) => session.id)
      .filter((id) => !(id in sceneCountsRef.current) && !inFlightSceneCountsRef.current.has(id));

    if (missingIds.length === 0) return undefined;

    for (const id of missingIds) {
      inFlightSceneCountsRef.current.add(id);
    }

    let active = true;
    fetchSessionSceneCounts(firestore, missingIds).then((counts) => {
      for (const id of missingIds) {
        inFlightSceneCountsRef.current.delete(id);
      }
      if (!active) return;

      const validCounts = {};
      for (const [id, count] of Object.entries(counts)) {
        if (typeof count === "number") {
          sceneCountsRef.current[id] = count;
          validCounts[id] = count;
        }
      }
      if (Object.keys(validCounts).length > 0) {
        setSceneCountsById((prev) => ({ ...prev, ...validCounts }));
      }
    });

    return () => {
      active = false;
    };
  }, [pagedSessions]);

  const selectedSession = sessions.find((session) => session.id === selectedId);
  const activeSessionCount = sessions.filter((session) => session.archived !== true).length;
  const archivedSessionCount = sessions.length - activeSessionCount;
  const selectedDisplayCode = selectedSession?.session_code ||
    legacySessionLabels.get(selectedId) ||
    "Sesión";
  const selectedTag = selectedSession?.tag;
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

  useEffect(() => {
    if (selectedSession && (selectedSession.archived === true) !== showArchived) {
      setSelectedId(
        visibleSessions.find((session) => (session.archived === true) === showArchived)?.id || "",
      );
    }
  }, [selectedSession, showArchived, visibleSessions]);

  function changeArchiveView(archived) {
    setShowArchived(archived);
    setCurrentPage(1);
    setSelectedId(
      sessions.find((session) => (session.archived === true) === archived)?.id || "",
    );
  }

  function requestDeleteSession() {
    if (!selectedSession) return;
    setDeleteTarget({
      type: "session",
      sessionId: selectedSession.id,
      displayCode: selectedDisplayCode,
      sceneCount: scenes.length,
      roundCount: totalRounds,
    });
    setDeleteStep(1);
    setDeleteConfirmText("");
    setDeleteError("");
  }

  function requestDeleteRound({ sceneId, sceneName, difficultyId, difficultyLabel, round, statusLabel }) {
    if (!selectedId || !round?._ref) return;
    setDeleteTarget({
      type: "round",
      sessionId: selectedId,
      sceneId,
      sceneName,
      difficultyId,
      difficultyLabel,
      round,
      roundDateLabel: dateLabel(round.ended_at),
      statusLabel,
    });
    setDeleteStep(1);
    setDeleteConfirmText("");
    setDeleteError("");
  }

  async function executeDelete() {
    if (!firestore || !deleteTarget || deletingEntry) return;
    setDeletingEntry(true);
    setDeleteError("");
    try {
      if (deleteTarget.type === "session") {
        const removedId = deleteTarget.sessionId;
        await removeSessionFromFirestore(firestore, removedId);
        delete sceneCountsRef.current[removedId];
        setDeleteTarget(null);
        await refreshSessions();
      } else {
        await removeRoundFromFirestore(firestore, {
          sessionId: deleteTarget.sessionId,
          sceneId: deleteTarget.sceneId,
          difficultyId: deleteTarget.difficultyId,
          roundRef: deleteTarget.round._ref,
        });
        delete sceneCountsRef.current[deleteTarget.sessionId];
        setDeleteTarget(null);
        setDetailRefreshKey((key) => key + 1);
      }
    } catch (error) {
      console.error("No se pudo eliminar el registro en Firebase.", error);
      setDeleteError(`No se pudo eliminar en Firebase. ${errorMessage(error)}`);
    } finally {
      setDeletingEntry(false);
    }
  }

  async function toggleSelectedArchive() {
    if (!selectedSession) return;
    const shouldArchive = selectedSession.archived !== true;
    setSavingArchive(true);
    setArchiveError("");
    try {
      if (shouldArchive) {
        await saveSessionArchive(firestore, selectedId);
      } else {
        await clearSessionArchive(firestore, selectedId);
      }
      await refreshSessions();
    } catch (error) {
      console.error("No se pudo actualizar el archivo de la sesión en Firebase.", error);
      setArchiveError(
        `No se pudo ${shouldArchive ? "archivar" : "restaurar"} la sesión. ${errorMessage(error)}`,
      );
    } finally {
      setSavingArchive(false);
    }
  }

  function openTagEditor() {
    setTagNameDraft(selectedTag?.name || "");
    setTagColorDraft(selectedTag?.color || "#a99cf2");
    setTagError("");
    setTagEditorOpen(true);
  }

  async function saveSessionTag(event) {
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
    setSavingTag(true);
    setTagError("");
    try {
      await writeSessionTag(firestore, selectedId, { name, color: tagColorDraft });
      setTagEditorOpen(false);
      await refreshSessions();
    } catch (error) {
      console.error("No se pudo guardar el tag en Firebase.", error);
      setTagError(`No se pudo guardar el tag en Firebase. ${errorMessage(error)}`);
    } finally {
      setSavingTag(false);
    }
  }

  async function removeSessionTag() {
    setSavingTag(true);
    setTagError("");
    try {
      await deleteSessionTag(firestore, selectedId);
      setTagEditorOpen(false);
      await refreshSessions();
    } catch (error) {
      console.error("No se pudo quitar el tag de Firebase.", error);
      setTagError(`No se pudo quitar el tag de Firebase. ${errorMessage(error)}`);
    } finally {
      setSavingTag(false);
    }
  }

  function updateCutoff(value) {
    setCutoffValue(value);
    setCurrentPage(1);
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
              title="Actualizar todas las sesiones"
              type="button"
            >
              {loadingSessions ? <span className="spinner small-spinner" /> : "↻"}
            </button>
          </div>
          <div className="session-view-switch" role="group" aria-label="Filtrar sesiones">
            <button
              aria-pressed={!showArchived}
              className={`quiet-button session-view-button${showArchived ? "" : " active"}`}
              onClick={() => changeArchiveView(false)}
              type="button"
            >
              Activas <span>{activeSessionCount}</span>
            </button>
            <button
              aria-pressed={showArchived}
              className={`quiet-button session-view-button${showArchived ? " active" : ""}`}
              onClick={() => changeArchiveView(true)}
              type="button"
            >
              Archivadas <span>{archivedSessionCount}</span>
            </button>
          </div>
          <label className="search-box">
            <span>⌕</span>
            <input
              placeholder="Buscar código, tag o fecha..."
              value={search}
              onChange={(event) => {
                setSearch(event.target.value);
                setCurrentPage(1);
              }}
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
            {filteredSessions.length} de {visibleSessions.length} {showArchived ? "archivadas" : "activas"} · {sessions.length} en total
          </p>
          <div className="scene-legend" aria-label="Leyenda de escenas por sesión">
            <span className="scene-legend-item">
              <span className="scene-legend-dot scene-count-1" />1 escena
            </span>
            <span className="scene-legend-item">
              <span className="scene-legend-dot scene-count-2" />2 escenas
            </span>
            <span className="scene-legend-item">
              <span className="scene-legend-dot scene-count-3" />3 escenas
            </span>
          </div>
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
                      tag={session.tag}
                      sceneCount={sceneCountsById[session.id]}
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
                {search
                  ? "No hay sesiones que coincidan."
                  : showArchived
                    ? "No hay sesiones archivadas."
                    : "No hay sesiones activas."}
              </p>
            )}
          </div>
          {orderedSessions.length > 0 && (
            <div className="pagination-bar">
              <div className="pagination-summary">
                <span>
                  Mostrando {pageStartIndex + 1}–{Math.min(pageStartIndex + pageSize, orderedSessions.length)} de {orderedSessions.length}
                </span>
                <select
                  aria-label="Sesiones por página"
                  className="page-size-select"
                  onChange={(event) => {
                    setPageSize(Number(event.target.value));
                    setCurrentPage(1);
                  }}
                  value={pageSize}
                >
                  {PAGE_SIZE_OPTIONS.map((size) => (
                    <option key={size} value={size}>
                      {size} / pág.
                    </option>
                  ))}
                </select>
              </div>
              <div className="pagination-controls">
                <button
                  className="quiet-button"
                  disabled={safePage <= 1}
                  onClick={() => setCurrentPage((page) => Math.max(1, page - 1))}
                  type="button"
                >
                  ‹ Anterior
                </button>
                <span className="pagination-page-label">
                  Página {safePage} de {totalPages}
                </span>
                <button
                  className="quiet-button"
                  disabled={safePage >= totalPages}
                  onClick={() => setCurrentPage((page) => Math.min(totalPages, page + 1))}
                  type="button"
                >
                  Siguiente ›
                </button>
              </div>
            </div>
          )}
          <p className="refresh-time">
            {lastRefresh
              ? `Actualizado ${lastRefresh.toLocaleTimeString("es-CL")}`
          : "Pulsa actualizar para leer todas las sesiones."}
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
                    {selectedSession?.archived === true ? " · Archivada" : ""}
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
                  <button
                    className="quiet-button"
                    disabled={savingArchive}
                    onClick={toggleSelectedArchive}
                    type="button"
                  >
                    {savingArchive
                      ? "Guardando..."
                      : selectedSession?.archived === true
                        ? "Restaurar sesión"
                        : "Archivar sesión"}
                  </button>
                  <button className="quiet-button" onClick={openTagEditor} type="button">
                    {selectedTag ? "Editar tag" : "Añadir tag"}
                  </button>
                  <button
                    className="danger-button"
                    disabled={loadingDetails || deletingEntry}
                    onClick={requestDeleteSession}
                    type="button"
                  >
                    Eliminar sesión
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
              {archiveError && <p className="error-banner">{archiveError}</p>}
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
                      disabled={savingTag}
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
                      disabled={savingTag}
                    />
                  </label>
                  <p className="tag-help">El tag se guarda en Firebase y se comparte con todos.</p>
                  <button className="primary-button" disabled={savingTag} type="submit">
                    {savingTag ? "Guardando..." : "Guardar tag"}
                  </button>
                  {selectedTag && (
                    <button className="quiet-button" disabled={savingTag} onClick={removeSessionTag} type="button">
                      {savingTag ? "Guardando..." : "Quitar tag"}
                    </button>
                  )}
                  <button className="quiet-button" disabled={savingTag} onClick={() => setTagEditorOpen(false)} type="button">
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
                        onRequestDeleteRound={requestDeleteRound}
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
                                onDelete={(targetRound, statusLabel) =>
                                  requestDeleteRound({
                                    sceneId: null,
                                    sceneName: SCENES[targetRound.scenario_id] || "Estructura anterior",
                                    difficultyId: null,
                                    difficultyLabel: "Ronda sin clasificar",
                                    round: targetRound,
                                    statusLabel,
                                  })
                                }
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
      <DeleteConfirmModal
        confirmText={deleteConfirmText}
        deleting={deletingEntry}
        error={deleteError}
        onCancel={() => {
          if (!deletingEntry) setDeleteTarget(null);
        }}
        onConfirm={executeDelete}
        onConfirmTextChange={setDeleteConfirmText}
        onStepChange={(nextStep) => {
          setDeleteStep(nextStep);
          setDeleteError("");
          if (nextStep !== 3) setDeleteConfirmText("");
        }}
        step={deleteStep}
        target={deleteTarget}
      />
    </div>
  );
}
