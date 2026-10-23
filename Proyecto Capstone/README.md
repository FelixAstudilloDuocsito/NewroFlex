# NeuroFlex — Módulo de Realidad Virtual (Unity)

![Unity](https://img.shields.io/badge/Unity-2022.3+-brightgreen) ![VR](https://img.shields.io/badge/VR-XR%20Interaction%20Toolkit-blue) ![Estado](https://img.shields.io/badge/Estado-En%20desarrollo-yellow)

**NeuroFlex** es un sistema de evaluación cognitiva basado en realidad virtual. Este repositorio contiene el **cliente Unity en VR**, donde los participantes interactúan con escenarios inmersivos diseñados para estimular y medir distintas capacidades cognitivas.

---

## 📋 Tabla de Contenidos

- [Arquitectura del Proyecto](#arquitectura-del-proyecto)
- [Escenarios y Actividades](#escenarios-y-actividades)
  - [Recepción — Hub de Navegación](#1-recepción--hub-de-navegación)
  - [Biblioteca — Puzzle de Intercambio](#2-biblioteca--puzzle-de-intercambio-swap-puzzle)
  - [Juego de Memoria — Encontrar Pares](#3-juego-de-memoria-encontrar-pares)
  - [Parque — Alimentar Palomas](#4-parque--alimentar-palomas)
- [Mecánicas Transversales](#mecánicas-transversales)
  - [Tutoriales en Video](#tutoriales-en-video)
  - [Preguntas de Evaluación Post-Actividad](#preguntas-de-evaluación-post-actividad)
  - [Feedback Visual Estelar](#feedback-visual-estelar)
- [Métricas y Telemetría](#métricas-y-telemetría)
- [Stack Tecnológico](#stack-tecnológico)
- [Estructura del Proyecto](#estructura-del-proyecto)
- [Requisitos](#requisitos)
- [Cómo Empezar](#cómo-empezar)
- [Contribuir](#contribuir)
- [Autores](#autores)
- [Licencia](#licencia)

---

## Arquitectura del Proyecto

NeuroFlex se organiza como un **monorepositorio** con tres componentes principales:

```
NewroFlex/
├── Proyecto Capstone/           ← ◀ ESTE MÓDULO (Unity + VR)
│   └── Assets/
│       ├── Scenes/              → Escenas del juego
│       ├── Scripts/             → Lógica gameplay y UI
│       └── ...                  → Assets 3D, texturas, modelos
│
├── neuroflex-dashboard/         → App mobile/web (Expo/React Native)
│   └── Muestra las métricas recopiladas durante las sesiones
│
└── entrenamiento_modelo_ia/     → Pipeline Python de IA (Ollama)
    └── Procesa telemetría y genera resúmenes cognitivos
```

### Flujo de Datos

```
[ Participante en VR ] ──(telemetría)──→ [ Dashboard (Expo) ]
       │                                        │
       │                                 [ Visualización ]
       │
       └──(sesiones)──→ [ Pipeline IA (Ollama) ]
                            │
                            └──→ [ Resúmenes cognitivos ]
```

Las sesiones de juego registran métricas de rendimiento que son visualizadas en vivo desde el dashboard y posteriormente analizadas por el módulo de IA para generar reportes descriptivos del progreso del participante.

---

## Escenarios y Actividades

Cada escenario representa una actividad cognitiva distinta. El participante navega entre ellas desde la Recepción. Cada escena incluye un **tutorial opcional** y una **pregunta de evaluación** al completar la actividad.

### 1. Recepción — Hub de Navegación

**Archivo:** `Assets/Scenes/Recepción.unity`

Sala central donde el participante elige a qué escenario desea ingresar. Funciona como un menú espacial en VR con imágenes interactivas que, al seleccionarse, disparan la carga del escenario correspondiente con una transición de fundido a negro.

- Cada imagen representa un escenario disponible.
- Efecto *hover* en los botones para feedback visual.
- Transición suave con `CanvasGroup` (fade) antes de cargar la escena destino.

### 2. Biblioteca — Puzzle de Intercambio (Swap Puzzle)

**Archivo:** `Assets/Scenes/Puzzle Libreria.unity`  
**Script principal:** `SwapPuzzleManager.cs`

Un puzzle de piezas desordenadas que el participante debe **intercambiar de posición** arrastrándolas con los controles VR hasta que cada pieza quede en la ranura correcta.

#### Mecánica

- Cada pieza se puede agarrar (grab) y soltar (release).
- Al soltarla cerca de otra ranura, ambas piezas **intercambian sus posiciones** suavemente.
- Si se suelta lejos de cualquier ranura, la pieza **regresa a su posición original**.
- El puzzle se completa cuando cada pieza ocupa la ranura que le corresponde por ID.

#### Dificultades

| Nivel | Descripción |
|-------|-------------|
| 1     | Piezas y slots iniciales; tutorial disponible. |
| 2     | Más piezas o disposición diferente. |
| 3     | Dificultad máxima. |

Al completar todos los niveles, se muestra un panel de victoria.

#### Tutorial

Al iniciar el nivel 1, se reproduce un **video tutorial** incrustado (`TutorialPanelController`). El participante debe confirmar haberlo visto antes de que comience la partida.

### 3. Juego de Memoria — Encontrar Pares

**Archivo:** `Assets/Scenes/Juego De Memoria.unity`  
**Scripts:** `MemoryGameManager.cs`, `MemoryCard.cs`

*(Este juego se conoce popularmente como *Memory*, *Concentration* o *Parejas*)*

#### Mecánica

1. Se muestran todas las cartas **boca arriba** durante un tiempo configurable (vista previa, por defecto 2 segundos).
2. Las cartas se voltean **boca abajo** y se **barajan** sus posiciones en la mesa.
3. El participante voltea dos cartas por turno:
   - **Si son iguales:** se marcan como emparejadas, saltan estrellas de celebración y el par queda visible.
   - **Si son distintas:** se vuelven a ocultar tras un breve momento para que el participante memorice su ubicación.
4. El juego termina cuando todos los pares han sido encontrados.

#### Detalles Técnicos

- Interacción mediante `XR Simple Interactable` (toque/selección en VR).
- Giro animado en el eje configurable (por defecto 180° en X).
- Barajado aleatorio de posiciones al inicio.
- Feedback visual con estrellas (`StarFeedback.cs`) que vuelan en abanico hacia arriba al acertar un par.

### 4. Parque — Alimentar Palomas

**Archivo:** `Assets/Scenes/Parque-Palomas.unity`  
**Script asociado:** `ReturnToSlot.cs`

Escenario exterior ambientado en un **parque**, donde el participante debe **alimentar palomas** interactuando con objetos del entorno.

#### Mecánica

- El participante agarra objetos (comida) y los acerca a las palomas u otros puntos designados.
- Si el objeto se suelta sin completar la acción, **regresa suavemente a su lugar original** gracias al script `ReturnToSlot`.
- La actividad concluye al cumplir los objetivos de alimentación.

> **Nota:** Esta escena comparte el sistema de agarre y retorno (`XRGrabInteractable` + `ReturnToSlot`) con otras actividades, y puede configurarse con diferentes niveles de dificultad al igual que el resto de escenarios.

### Escenarios Adicionales (en desarrollo/planeados)

- **Supermercado** (`Assets/Supermercado/`) — Modelo 3D de un mercado con estanterías y productos. Potencial escenario de compra/asociación de objetos.

---

## Mecánicas Transversales

### Tutoriales en Video

Controlador: `TutorialPanelController.cs`

Cada escenario puede reproducir un **video tutorial** al iniciarse. El video se muestra en un panel flotante dentro de la escena VR, con posibilidad de loop. El participante confirma visualizarlo y el panel se cierra para dar paso a la actividad.

### Preguntas de Evaluación Post-Actividad

Controlador: `MemoryQuestionManager.cs` / `QuestionButtonFeedback.cs`

Después de completar ciertos niveles, el sistema oculta el tablero de juego y presenta una **pregunta de opción múltiple** (4 alternativas) relacionada con la actividad recién realizada.

- Ejemplo: *"¿De qué color era el cielo en el puzzle anterior?"*
- **Acierto:** feedback verde + avance al siguiente nivel.
- **Error:** feedback rojo + el participante puede intentar de nuevo.
- El sistema registra qué opción se eligió (correcta/incorrecta) como métrica adicional.

Los botones de respuesta tienen feedback visual de tres estados: oscuro al presionar, verde si es correcto, rojo si es incorrecto.

### Feedback Visual Estelar

Controlador: `StarFeedback.cs`

Cuando el participante acierta un par en el Memory o completa un hito, aparecen **estrellas** que vuelan desde el punto de acierto en formación de abanico, siempre orientadas hacia la cámara, y se desvanecen progresivamente. Refuerzo visual positivo inmediato.

---

## Métricas y Telemetría

Cada sesión de juego genera datos de rendimiento que son enviados al dashboard y al pipeline de IA. Las métricas principales incluyen:

| Métrica | Descripción | Unidad |
|---------|-------------|--------|
| **Accuracy** | Precisión (aciertos / total) | % |
| **Response Time** | Tiempo medio de respuesta | ms |
| **Gaze Deviation** | Desviación de la mirada | % |
| **Controller Tremor** | Índice de temblor del control | índice |
| **Session Duration** | Duración total de la sesión | segundos |

Además, por cada ronda se registran:

- Estado de la ronda (`completed`, `abandoned`, `interrupted`)
- Nivel de dificultad
- Distracciones activas (tipo, intensidad, duración)
- Ayudas utilizadas
- Tiempo activo (excluyendo pausas)
- Movimientos incorrectos

### Modelo de Datos

```json
{
  "participantId": "p001",
  "sessionId": "s001",
  "scenarioId": "puzzle",
  "difficultyLevel": 1,
  "status": "completed",
  "totalPieces": 23,
  "correctPlacements": 21,
  "incorrectPlacements": 2,
  "activeTimeMs": 60445,
  "distractionsEnabled": false,
  "aidsUsed": 1
}
```

Estos datos alimentan el **pipeline de IA** (`entrenamiento_modelo_ia/`) que, mediante distintos *prompts* o *semillas*, genera resúmenes descriptivos del rendimiento —siempre sin interpretación clínica— usando modelos locales vía Ollama.

---

## Stack Tecnológico

| Componente | Tecnología |
|------------|------------|
| **Motor de Juego** | Unity 2022.3+ |
| **Realidad Virtual** | XR Interaction Toolkit (XRI) |
| **Lenguaje** | C# (Unity) |
| **Modelado 3D** | Blender, modelos .fbx/.obj/.stl |
| **Dashboard** | Expo (React Native) + TypeScript |
| **IA** | Python + Ollama (Qwen3, etc.) |
| **UI en VR** | TextMeshPro, Unity UI, Canvas |

---

## Estructura del Proyecto

```
Assets/
├── Scenes/                   → Escenas .unity organizadas por actividad
│   ├── Recepción.unity
│   ├── Puzzle Libreria.unity
│   ├── Juego De Memoria.unity
│   └── Parque-Palomas.unity
│
├── Scripts/                  → Lógica del juego (C#)
│   ├── SwapPuzzleManager.cs        → Gestor del puzzle de intercambio
│   ├── PuzzlePiece.cs              → Comportamiento de cada pieza
│   ├── MemoryGameManager.cs        → Gestor del juego de memoria
│   ├── MemoryCard.cs               → Lógica de cada carta
│   ├── MemoryQuestionManager.cs    → Preguntas post-actividad
│   ├── QuestionButtonFeedback.cs   → Feedback visual de botones
│   ├── ImageSceneLoaderVR.cs       → Carga de escenas con transición
│   ├── TutorialPanelController.cs  → Reproductor de tutoriales
│   ├── ReturnToSlot.cs             → Retorno suave al soltar objetos
│   └── StarFeedback.cs             → Efecto de estrellas
│
├── Recepcion/               → Assets gráficos del hub
├── Libreria/                → Assets 3D y tutoriales de la biblioteca
│   └── Video_Tutorial_Libreria/
├── Park/                    → Modelos 3D del parque (parklow, treelow)
├── Supermercado/            → Modelos 3D del supermercado
├── Prefabs/                 → Prefabs reutilizables
├── Texturas/                → Texturas y materiales
├── Decoration/              → Decoración adicional
├── SkyboxHandPainted/       → Cielos y fondos
└── Samples/                 → Samples de Unity
```

---

## Requisitos

- **Unity Editor** 2022.3 o superior (recomendado: 2022.3 LTS)
- **XR Interaction Toolkit** (incluido en el proyecto)
- **Git LFS** para assets 3D pesados
- **(Opcional) Ollama** si se trabaja con el pipeline de IA

---

## Cómo Empezar

```bash
# 1. Clonar el repositorio completo
git clone https://github.com/tu-org/NewroFlex.git
cd "NewroFlex/Proyecto Capstone"

# 2. Abrir en Unity
#    - Unity Hub → Add → seleccionar la carpeta "Proyecto Capstone"
#    - Esperar a que se importen los assets (primera vez puede tomar varios minutos)

# 3. Escena por defecto
#    - Abrir Assets/Scenes/Recepción.unity
#    - Presionar Play en el editor
```

### Notas de Desarrollo

- Los modelos 3D pesados (`Park/`, `Supermercado/`, `Libreria/`) usan formatos `.fbx`, `.blend`, `.stl`, `.obj`. Se importan automáticamente.
- Las escenas se cargan de forma asíncrona con `SceneManager.LoadSceneAsync()` y una transición fade.
- El proyecto incluye configuración de VS Code (`Proyecto Capstone/.vscode/`) con extensiones recomendadas para C# en Unity.

---

## Contribuir

1. Crea una rama desde `develop`: `git checkout -b feat/mi-feature`
2. Desarrolla y prueba en Unity.
3. Asegúrate de que los scripts existentes sigan funcionando.
4. Envía un Pull Request describiendo los cambios.

---

## Autores

Proyecto desarrollado como **Capstone** por:

- Félix Astudillo
- Sebastián Calderón
- Alex Armijo

---

## Licencia

Proyecto académico — todos los derechos reservados.