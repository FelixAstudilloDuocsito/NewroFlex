# NeuroFlex Firebase Viewer

Aplicación de escritorio para explorar las sesiones de prueba de NeuroFlex en
Cloud Firestore y asignarles tags compartidos. Es independiente del dashboard
Expo del proyecto.

La interfaz consulta todas las sesiones y las muestra paginadas en la barra
lateral (por defecto 50 por página, configurable entre 25, 50 y 100); carga las
escenas, dificultades y rondas sólo cuando se selecciona una sesión, y consulta
en la página actual cuántas escenas tiene cada sesión para mostrar su ícono en
rojo (1 escena), amarillo (2 escenas) o verde (3 escenas). Los tags
se guardan en el campo `tag` del documento `sessions/{sessionId}` y se leen
desde Firestore; así quedan disponibles para todos los usuarios del visor. La
fecha de corte y los grupos visuales se guardan localmente en este equipo.
Las sesiones pueden archivarse y restaurarse desde Firestore; archivarlas sólo
las mueve al listado separado, sin borrar métricas ni rondas. También es posible
eliminar sesiones completas o rondas individuales mediante un flujo de triple
confirmación.

## Requisitos

- Node.js 20.19 o superior.
- Una aplicación Web registrada en el proyecto Firebase `newroflex`.
- Reglas actuales de Firestore que permitan leer las sesiones sin iniciar sesión.

## Configuración

1. En Firebase Console, abre **Configuración del proyecto → Tus apps** y registra
   una app Web en el proyecto existente. No crees otro proyecto.
2. Desde esta carpeta, crea `.env.local` a partir de la plantilla:

   ```dotenv
   VITE_FIREBASE_API_KEY=...
   VITE_FIREBASE_AUTH_DOMAIN=...
   VITE_FIREBASE_PROJECT_ID=newroflex
   VITE_FIREBASE_APP_ID=...
   ```

   En PowerShell puedes copiar la plantilla con
   `Copy-Item .env.example .env.local`; luego reemplaza esos valores con la
   configuración de la aplicación Web registrada.
3. Comprueba que las reglas de Firestore permitan leer sesiones, actualizar
   sus tags y estado de archivado, y eliminar sesiones o rondas desde el visor,
   además de las escrituras de Unity. Unity reserva códigos de sesión mediante
   una transacción sobre `metadata/sessionCodeCounter` y necesita poder leer y
   escribir ese documento.

La configuración Web de Firebase se incluye en el paquete del cliente y no es
una credencial de servidor; el acceso real depende de las reglas de Firestore.
No pongas contraseñas ni claves de cuenta de servicio en `.env.local`.

## Ejecutar en escritorio

Desde esta carpeta:

```powershell
npm install
npm run dev
```

`npm run dev` abre una ventana de escritorio con recarga durante el desarrollo.
`npm run desktop` construye la interfaz y la abre sin el servidor de desarrollo.
La configuración `.env.local` se lee al compilar; reinicia o recompila después
de modificarla.

## Estructura mostrada

El visor lee el esquema actual:

```text
sessions/{sessionId}
  scenes/{scenarioId}
    difficulties/{difficulty}
      rounds/{roundId}
```

Las rondas que aún estén en la colección antigua `sessions/{sessionId}/rounds`
se muestran aparte como **Estructura anterior**. El visor no las migra.

Cada `sessionId` distinto se muestra como una sesión separada; las nueve rondas
del botón de prueba comparten un ID `s-test-...` y quedan agrupadas en esa
sesión. Al iniciar Unity, el cargador también vuelve a sincronizar resultados
que estaban guardados localmente. Por eso pueden aparecer sesiones `s-...`
anteriores aunque se haya limpiado Firestore.

Unity asigna un código global legible a cada sesión nueva o sincronizada,
guardado como `session_code` (por ejemplo, `sesion-A001`). El ID técnico del
documento Firestore se conserva internamente. La secuencia avanza por bloques de
999 (`A001`–`A999`, `B001`–`B999`, etc.; después de `Z999`, continúa con `AA001`).
Las sesiones antiguas que aún no tengan `session_code` se muestran con una
etiqueta local temporal. La lista se agrupa por `session_started_at` usando la
hora local del equipo, así una resincronización del historial no cambia el día
original de la sesión. Las sesiones antiguas que aún no tienen
`session_started_at` se separan en **Historial anterior** en vez de atribuirlas
a hoy; `updated_at` se muestra sólo como fecha de última carga de referencia.
El campo **Fecha y hora de corte** permite separar la lista entre historial
anterior y pruebas desde el corte (incluido). Al configurarlo, se compara
primero con `session_started_at`; sólo si falta se usa `updated_at` de Firestore.
Así, una resincronización no reclasifica las sesiones que ya tienen su fecha
original; las que aún no la tienen usan la última carga hasta que Unity la
complete. Se conservan los subgrupos por día; la fecha se interpreta en la zona
horaria local y se guarda sólo en este equipo.
Los tags y el archivado se comparten en Firebase. La lista muestra las sesiones
activas y archivadas por separado; archivar no borra datos y se puede revertir.
Las métricas se
ordenan por escena, los campos vacíos se ocultan por defecto y
pueden mostrarse con el botón correspondiente.
Las escenas se presentan como una lista vertical en el orden Biblioteca,
Memoria y Palomas. Cada escena y dificultad se puede desplegar para consultar
sus rondas y métricas sin ocupar toda la pantalla.

## Referencias

- [Firebase Quickstart Samples for Web](https://github.com/firebase/quickstart-js)
  (Apache-2.0): referencia para el SDK modular de Firebase Web.
- [Rowy](https://github.com/rowyio/rowy): se evaluó como panel visual de Firestore,
  pero incluye funciones CMS y escritura que no hacen falta para este visor temporal.
- [Configuración del SDK web de Firebase](https://firebase.google.com/docs/web/setup).
- [Reglas de seguridad de Cloud Firestore](https://firebase.google.com/docs/firestore/security/get-started).

La interfaz de este visor es código propio; no se copió la interfaz de otro
proyecto. Se eligió una app pequeña a medida frente a un panel administrativo
general porque el caso sólo requiere lectura de sesiones y actualización manual.
