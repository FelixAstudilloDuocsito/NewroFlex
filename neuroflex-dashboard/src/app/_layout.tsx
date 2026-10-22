import { Stack } from 'expo-router';

export default function RootLayout() {
  return (
    <Stack
      screenOptions={{
        headerShown: false, // Desactiva la barra superior flotante y los encabezados
      }}
    />
  );
}