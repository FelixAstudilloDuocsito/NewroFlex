import {
  collection,
  deleteDoc,
  deleteField,
  doc,
  documentId,
  getDocs,
  limit,
  orderBy,
  query,
  startAfter,
  updateDoc,
  writeBatch,
} from "firebase/firestore";

export const SCENES = {
  pigeons: "Palomas",
  memory: "Memoria",
  puzzle: "Biblioteca",
};

const SCENE_ORDER = ["puzzle", "memory", "pigeons"];
const SESSION_PAGE_SIZE = 500;

function timestampMillis(timestamp) {
  return timestamp && typeof timestamp.toDate === "function"
    ? timestamp.toDate().getTime()
    : 0;
}

function documentRecord(snapshot) {
  return { id: snapshot.id, ...snapshot.data(), _ref: snapshot.ref };
}

export async function fetchSessions(firestore) {
  const sessionsCollection = collection(firestore, "sessions");
  const sessions = [];
  let pageQuery = query(
    sessionsCollection,
    orderBy(documentId()),
    limit(SESSION_PAGE_SIZE),
  );

  while (true) {
    const result = await getDocs(pageQuery);
    sessions.push(...result.docs.map(documentRecord));
    if (result.docs.length < SESSION_PAGE_SIZE) break;

    pageQuery = query(
      sessionsCollection,
      orderBy(documentId()),
      startAfter(result.docs[result.docs.length - 1]),
      limit(SESSION_PAGE_SIZE),
    );
  }

  return sessions
    .sort((left, right) => timestampMillis(right.updated_at) - timestampMillis(left.updated_at));
}

export async function saveSessionTag(firestore, sessionId, tag) {
  await updateDoc(doc(firestore, "sessions", sessionId), { tag });
}

export async function removeSessionTag(firestore, sessionId) {
  await updateDoc(doc(firestore, "sessions", sessionId), { tag: deleteField() });
}

export async function archiveSession(firestore, sessionId) {
  await updateDoc(doc(firestore, "sessions", sessionId), { archived: true });
}

export async function restoreSession(firestore, sessionId) {
  await updateDoc(doc(firestore, "sessions", sessionId), { archived: deleteField() });
}

export async function fetchSessionSceneCount(firestore, sessionId) {
  const sessionRef = doc(firestore, "sessions", sessionId);
  const sceneSnapshots = await getDocs(collection(sessionRef, "scenes"));
  if (!sceneSnapshots.empty) {
    return sceneSnapshots.size;
  }

  const legacySnapshot = await getDocs(collection(sessionRef, "rounds"));
  if (legacySnapshot.empty) return 0;

  const legacySceneIds = new Set();
  for (const roundDoc of legacySnapshot.docs) {
    const scenarioId = roundDoc.data()?.scenario_id;
    if (scenarioId) legacySceneIds.add(scenarioId);
  }
  return legacySceneIds.size || 1;
}

export async function fetchSessionSceneCounts(firestore, sessionIds) {
  const entries = await Promise.all(
    sessionIds.map(async (sessionId) => {
      try {
        const count = await fetchSessionSceneCount(firestore, sessionId);
        return [sessionId, count];
      } catch (error) {
        console.error(`No se pudo leer la cantidad de escenas de ${sessionId}.`, error);
        return [sessionId, null];
      }
    }),
  );

  return Object.fromEntries(entries);
}

async function deleteReferencesInBatches(firestore, refs) {
  const BATCH_LIMIT = 450;
  for (let index = 0; index < refs.length; index += BATCH_LIMIT) {
    const batch = writeBatch(firestore);
    for (const ref of refs.slice(index, index + BATCH_LIMIT)) {
      batch.delete(ref);
    }
    await batch.commit();
  }
}

export async function deleteRound(firestore, { sessionId, sceneId, difficultyId, roundRef }) {
  if (!roundRef) {
    throw new Error("No se encontró la referencia de la ronda en Firestore.");
  }

  await deleteDoc(roundRef);

  if (sessionId && sceneId && difficultyId) {
    const sceneRef = doc(firestore, "sessions", sessionId, "scenes", sceneId);
    const difficultyRef = doc(sceneRef, "difficulties", difficultyId);
    const remainingRounds = await getDocs(query(collection(difficultyRef, "rounds"), limit(1)));
    if (remainingRounds.empty) {
      await deleteDoc(difficultyRef);
      const remainingDifficulties = await getDocs(query(collection(sceneRef, "difficulties"), limit(1)));
      if (remainingDifficulties.empty) {
        await deleteDoc(sceneRef);
      }
    }
  }
}

export async function deleteSession(firestore, sessionId) {
  const sessionRef = doc(firestore, "sessions", sessionId);
  const refsToDelete = [];

  const sceneSnapshots = await getDocs(collection(sessionRef, "scenes"));
  for (const sceneSnapshot of sceneSnapshots.docs) {
    const difficultySnapshots = await getDocs(collection(sceneSnapshot.ref, "difficulties"));
    for (const difficultySnapshot of difficultySnapshots.docs) {
      const roundSnapshots = await getDocs(collection(difficultySnapshot.ref, "rounds"));
      for (const roundSnapshot of roundSnapshots.docs) {
        refsToDelete.push(roundSnapshot.ref);
      }
      refsToDelete.push(difficultySnapshot.ref);
    }
    refsToDelete.push(sceneSnapshot.ref);
  }

  const legacyRoundSnapshots = await getDocs(collection(sessionRef, "rounds"));
  for (const legacyRoundSnapshot of legacyRoundSnapshots.docs) {
    refsToDelete.push(legacyRoundSnapshot.ref);
  }

  refsToDelete.push(sessionRef);
  await deleteReferencesInBatches(firestore, refsToDelete);
}

export async function fetchSessionRounds(firestore, sessionId) {
  const sessionRef = doc(firestore, "sessions", sessionId);
  const sceneSnapshots = await getDocs(collection(sessionRef, "scenes"));

  const scenes = await Promise.all(
    sceneSnapshots.docs.map(async (sceneSnapshot) => {
      const scene = documentRecord(sceneSnapshot);
      const difficultySnapshots = await getDocs(
        collection(sceneSnapshot.ref, "difficulties"),
      );

      const difficulties = await Promise.all(
        difficultySnapshots.docs.map(async (difficultySnapshot) => {
          const difficulty = documentRecord(difficultySnapshot);
          const roundsSnapshot = await getDocs(
            collection(difficultySnapshot.ref, "rounds"),
          );

          const rounds = roundsSnapshot.docs
            .map(documentRecord)
            .sort((left, right) => timestampMillis(right.ended_at) - timestampMillis(left.ended_at));

          return { ...difficulty, rounds };
        }),
      );

      difficulties.sort((left, right) => Number(left.id) - Number(right.id));
      return { ...scene, difficulties };
    }),
  );

  scenes.sort((left, right) => {
    const leftId = left.scenario_id || left.id;
    const rightId = right.scenario_id || right.id;
    const leftRank = SCENE_ORDER.indexOf(leftId);
    const rightRank = SCENE_ORDER.indexOf(rightId);
    if (leftRank !== rightRank) {
      if (leftRank === -1) return 1;
      if (rightRank === -1) return -1;
      return leftRank - rightRank;
    }

    const leftName = SCENES[leftId] || left.scenario_name || leftId;
    const rightName = SCENES[rightId] || right.scenario_name || rightId;
    return leftName.localeCompare(rightName, "es");
  });

  const legacySnapshot = await getDocs(collection(sessionRef, "rounds"));
  const legacyRounds = legacySnapshot.docs
    .map(documentRecord)
    .sort((left, right) => timestampMillis(right.ended_at) - timestampMillis(left.ended_at));

  return { scenes, legacyRounds };
}
