import { fetchSessions, SessionItem } from '@/services/api';
import AsyncStorage from '@react-native-async-storage/async-storage';
import { useCallback, useEffect, useState } from 'react';

export interface SessionData extends SessionItem {
  tenant_id?: string;
}

export function useSessions(participanteId: string) {
  const [sessions, setSessions] = useState<SessionData[]>([]);
  const [loading, setLoading] = useState<boolean>(true);
  const [error, setError] = useState<string | null>(null);

  const fetchSessionsData = useCallback(async () => {
    try {
      setError(null);
      
      const storedSession = await AsyncStorage.getItem('user_session');
      if (!storedSession) {
        throw new Error('No existe una sesión activa. Redirigiendo a inicio de sesión.');
      }

      const { token } = JSON.parse(storedSession);
      const data = await fetchSessions(token, participanteId);
      setSessions(data);
    } catch (err: any) {
      setError(err.message || 'Error al consultar sesiones protegidas en AWS');
    } finally {
      setLoading(false);
    }
  }, [participanteId]);

  useEffect(() => {
    fetchSessionsData();
  }, [fetchSessionsData]);

  return { sessions, loading, error, refetch: fetchSessionsData };
}