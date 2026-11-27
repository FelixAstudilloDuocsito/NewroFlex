import {
  collection,
  documentId,
  deleteField,
  doc,
  getDocs,
  limit,
  orderBy,
  query,
  startAfter,
  updateDoc,
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
