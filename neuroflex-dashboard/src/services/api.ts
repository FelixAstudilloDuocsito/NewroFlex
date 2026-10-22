import { AWS_CONFIG } from '../constants/config';

export interface SessionItem {
  session_id: string;
  participante_id: string;
  tenant_id?: string;
  experiencia?: string;
  duracion_seg?: number;
  scene_config_id?: string;
  metricas?: {
    score?: number;
  };
}

export async function fetchSessions(token: string, participanteId: string = 'P001'): Promise<SessionItem[]> {
  const url = `${AWS_CONFIG.API_URL}/sessions?participante_id=${participanteId}`;
  const cleanToken = token.replace(/^Bearer\s+/i, '').trim();

  try {
    const response = await fetch(url, {
      method: 'GET',
      headers: {
        'Authorization': cleanToken,
        'Content-Type': 'application/json',
      },
    });

    if (!response.ok) {
      const errorText = await response.text();
      throw new Error(`Error HTTP ${response.status}: ${errorText || 'No autorizado'}`);
    }

    const data = await response.json();
    
    // AWS Lambda devuelve {"items": [...]}, mapeamos correctamente la respuesta
    return Array.isArray(data) ? data : (data.items || data.sessions || []);
    
  } catch (error: any) {
    if (error.name === 'TypeError' && error.message === 'Failed to fetch') {
      throw new Error('Bloqueo CORS o endpoint inalcanzable. Revisa la Lambda.');
    }
    throw error;
  }
}