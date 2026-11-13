import {
  collection,
  doc,
  getDocs,
  limit,
  orderBy,
  query,
} from "firebase/firestore";

export const SCENES = {
  pigeons: "Palomas",
  memory: "Memoria",
  puzzle: "Biblioteca",
};

function timestampMillis(timestamp) {
  return timestamp && typeof timestamp.toDate === "function"
    ? timestamp.toDate().getTime()
    : 0;
}

function documentRecord(snapshot) {
  return { id: snapshot.id, ...snapshot.data(), _ref: snapshot.ref };
}

export async function fetchSessions(firestore) {
  const result = await getDocs(
    query(
      collection(firestore, "sessions"),
      orderBy("updated_at", "desc"),
      limit(50),
    ),
  );

  return result.docs
    .map(documentRecord)
    .sort((left, right) => timestampMillis(right.updated_at) - timestampMillis(left.updated_at));
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
    const leftName = SCENES[left.id] || left.scenario_name || left.id;
    const rightName = SCENES[right.id] || right.scenario_name || right.id;
    return leftName.localeCompare(rightName, "es");
  });

  const legacySnapshot = await getDocs(collection(sessionRef, "rounds"));
  const legacyRounds = legacySnapshot.docs
    .map(documentRecord)
    .sort((left, right) => timestampMillis(right.ended_at) - timestampMillis(left.ended_at));

  return { scenes, legacyRounds };
}
