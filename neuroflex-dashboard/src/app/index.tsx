import { Ionicons } from '@expo/vector-icons';
import AsyncStorage from '@react-native-async-storage/async-storage';
import { router } from 'expo-router';
import { useEffect, useState } from 'react';
import {
  ActivityIndicator,
  Modal,
  RefreshControl,
  SafeAreaView,
  ScrollView,
  StatusBar,
  StyleSheet,
  Text,
  TouchableOpacity,
  View,
} from 'react-native';
import { useSessions } from '../hooks/useSessions';

type UserRole = 'Admin Plataforma' | 'Admin Institución' | 'Especialista' | 'Auditor';
type TabType = 'Métricas' | 'Sesiones VR' | 'Costos y Nube';

interface SystemMetrics {
  instituciones: number;
  visoresSimultaneos: number;
  profesionalesSimultaneos: number;
  latenciaP95: string;
  disponibilidad: string;
  costoPorSesion: string;
  regionActiva: string;
  cumplimientoLey21719: string;
}

interface TabItem {
  key: TabType;
  label: string;
  iconActive: keyof typeof Ionicons.glyphMap;
  iconInactive: keyof typeof Ionicons.glyphMap;
}

export default function DashboardScreen() {
  const [activeRole, setActiveRole] = useState<UserRole>('Admin Plataforma');
  const [activeTab, setActiveTab] = useState<TabType>('Métricas');
  const [isCheckingAuth, setIsCheckingAuth] = useState(true);
  const [userEmail, setUserEmail] = useState('');
  const [refreshing, setRefreshing] = useState(false);

  const { sessions, loading: sessionsLoading, error: sessionsError, refetch } = useSessions('P001');

  const [aiModalVisible, setAiModalVisible] = useState(false);
  const [loadingAI, setLoadingAI] = useState(false);

  useEffect(() => {
    checkSession();
  }, []);

  const checkSession = async () => {
    try {
      const session = await AsyncStorage.getItem('user_session');
      if (!session) {
        router.replace('/login');
      } else {
        const parsed = JSON.parse(session);
        setUserEmail(parsed.email || '');
      }
    } catch (e) {
      router.replace('/login');
    } finally {
      setIsCheckingAuth(false);
    }
  };

  const handleLogout = async () => {
    await AsyncStorage.removeItem('user_session');
    router.replace('/login');
  };

  const onRefresh = async () => {
    setRefreshing(true);
    if (refetch) {
      await refetch();
    }
    setRefreshing(false);
  };

  const getAiSummary = () => {
    switch (activeRole) {
      case 'Admin Plataforma':
        return {
          title: 'Desglose Ejecutivo de Infraestructura',
          metrics: [
            '• Rendimiento SLA (99.92%): Sobre el umbral crítico del 99.9%. Cero interrupciones en la última semana.',
            '• Latencia P95 (380 ms): Operación fluida en sa-east-1. Dentro del límite operativo aceptable (< 500 ms).',
            '• Carga de Dispositivos: 842 visores Meta Quest 3 sincronizados y distribuidos en 265 especialistas activos.',
            '• Estado de Cumplimiento: Cifrado AWS KMS al 100%, conforme con los requerimientos de la Ley 21.719.',
          ],
          insight: 'Sugerencia IA: El tráfico actual se mantiene óptimo. Se recomienda monitorear latencia si el número de visores supera los 1,000 activos en simultáneo.',
        };
      case 'Admin Institución':
        return {
          title: 'Desglose Institucional',
          metrics: [
            '• Disponibilidad de Red local: 99.85% de estabilidad durante sesiones concurrentes.',
            '• Dispositivos Asignados: 45 Visores Meta Quest 3 operativos en sede.',
            '• Sesiones Activas: 18 Especialistas conduciendo pruebas en tiempo real.',
          ],
          insight: 'Sugerencia IA: Programar ventana de mantenimiento preventivo para actualizar firmwares pendientes en visores de baja actividad.',
        };
      case 'Especialista':
        return {
          title: 'Desglose de Sesiones y Pacientes',
          metrics: [
            '• Estado de Conexión: Latencia baja (380 ms), ideal para biometría en tiempo real.',
            '• Monitoreo Cognitivo: 12 Pacientes evaluados en el turno actual sin anomalías.',
          ],
          insight: 'Sugerencia IA: Los patrones neuro-flexibles indican un rendimiento óptimo en las pruebas matutinas.',
        };
      case 'Auditor':
        return {
          title: 'Desglose de Auditoría y Cumplimiento',
          metrics: [
            '• Trazabilidad de Logs: 100% de los accesos a fichas clínicas registrados con hash inmutable.',
            '• Cifrado de Datos: Llaves KMS rotadas correctamente en el último período.',
          ],
          insight: 'Sugerencia IA: Sin desvíos detectados respecto al marco legal de protección de datos de salud.',
        };
    }
  };

  const handleOpenAI = () => {
    setLoadingAI(true);
    setAiModalVisible(true);
    setTimeout(() => {
      setLoadingAI(false);
    }, 600);
  };

  const systemMetrics: SystemMetrics = {
    instituciones: 50,
    visoresSimultaneos: 842,
    profesionalesSimultaneos: 265,
    latenciaP95: '380 ms',
    disponibilidad: '99.92%',
    costoPorSesion: '$0.0094 USD',
    regionActiva: 'sa-east-1 (São Paulo)',
    cumplimientoLey21719: '100% Cifrado KMS',
  };

  const roles: UserRole[] = [
    'Admin Plataforma',
    'Admin Institución',
    'Especialista',
    'Auditor',
  ];

  const tabItems: TabItem[] = [
    {
      key: 'Métricas',
      label: 'Métricas',
      iconActive: 'stats-chart',
      iconInactive: 'stats-chart-outline',
    },
    {
      key: 'Sesiones VR',
      label: 'Sesiones VR',
      iconActive: 'headset',
      iconInactive: 'headset-outline',
    },
    {
      key: 'Costos y Nube',
      label: 'Costos/Nube',
      iconActive: 'cloudy',
      iconInactive: 'cloudy-outline',
    },
  ];

  const currentSummary = getAiSummary();

  if (isCheckingAuth) {
    return (
      <View style={styles.loadingContainer}>
        <ActivityIndicator size="large" color="#38BDF8" />
      </View>
    );
  }

  return (
    <SafeAreaView style={styles.container}>
      <StatusBar barStyle="light-content" backgroundColor="#0F172A" />

      {/* Encabezado Superior */}
      <View style={styles.header}>
        <View style={{ flex: 1 }}>
          <Text style={styles.headerTitle}>NeuroFlex VR</Text>
          <Text style={styles.headerSubtitle}>{userEmail || 'Plataforma SaaS'}</Text>
        </View>

        <View style={styles.headerActions}>
          <TouchableOpacity
            style={styles.aiButton}
            onPress={handleOpenAI}
            activeOpacity={0.8}
          >
            <Text style={styles.aiButtonText}>IA</Text>
          </TouchableOpacity>

          <TouchableOpacity onPress={handleLogout} style={styles.logoutButton}>
            <Ionicons name="log-out-outline" size={20} color="#EF4444" />
          </TouchableOpacity>
        </View>
      </View>

      {/* Selector de Rol */}
      <View style={styles.roleContainer}>
        <Text style={styles.sectionLabel}>Rol Activo:</Text>
        <ScrollView horizontal showsHorizontalScrollIndicator={false}>
          {roles.map((role) => (
            <TouchableOpacity
              key={role}
              style={[
                styles.roleChip,
                activeRole === role && styles.roleChipActive,
              ]}
              onPress={() => setActiveRole(role)}
            >
              <Text
                style={[
                  styles.roleText,
                  activeRole === role && styles.roleTextActive,
                ]}
              >
                {role}
              </Text>
            </TouchableOpacity>
          ))}
        </ScrollView>
      </View>

      {/* Contenido Principal */}
      <ScrollView
        style={styles.content}
        contentContainerStyle={styles.scrollPadding}
        refreshControl={
          activeTab === 'Sesiones VR' ? (
            <RefreshControl refreshing={refreshing} onRefresh={onRefresh} tintColor="#38BDF8" />
          ) : undefined
        }
      >
        {activeTab === 'Métricas' && (
          <View style={styles.tabContent}>
            <Text style={styles.cardHeaderTitle}>Rendimiento Global y SLA</Text>

            <View style={styles.grid}>
              <View style={styles.card}>
                <Text style={styles.cardValue}>{systemMetrics.disponibilidad}</Text>
                <Text style={styles.cardLabel}>Disponibilidad (SLA ≥ 99.9%)</Text>
              </View>
              <View style={styles.card}>
                <Text style={styles.cardValue}>{systemMetrics.latenciaP95}</Text>
                <Text style={styles.cardLabel}>Latencia P95 (&lt; 500 ms)</Text>
              </View>
              <View style={styles.card}>
                <Text style={styles.cardValue}>{String(systemMetrics.visoresSimultaneos)}</Text>
                <Text style={styles.cardLabel}>Visores Meta Quest 3</Text>
              </View>
              <View style={styles.card}>
                <Text style={styles.cardValue}>{String(systemMetrics.profesionalesSimultaneos)}</Text>
                <Text style={styles.cardLabel}>Especialistas Conectados</Text>
              </View>
            </View>

            <View style={styles.infoBox}>
              <Text style={styles.infoTitle}>Aislamiento Multi-tenant y Celda</Text>
              <Text style={styles.infoDetail}>Región AWS: {systemMetrics.regionActiva}</Text>
              <Text style={styles.infoDetail}>Seguridad: {systemMetrics.cumplimientoLey21719}</Text>
              <Text style={styles.infoDetail}>Ley 21.719: Datos cifrados por institución con AWS KMS.</Text>
            </View>
          </View>
        )}

        {activeTab === 'Sesiones VR' && (
          <View style={styles.tabContent}>
            <Text style={styles.cardHeaderTitle}>
              Registro de Sesiones (AWS DynamoDB - P001) ({sessions.length})
            </Text>

            {sessionsLoading && !refreshing ? (
              <View style={styles.centerContent}>
                <ActivityIndicator size="large" color="#38BDF8" />
                <Text style={styles.loadingText}>Consultando telemetría en caliente...</Text>
              </View>
            ) : sessionsError && !refreshing ? (
              <View style={styles.errorBox}>
                <Text style={styles.errorTitle}>Error de Conexión con la API</Text>
                <Text style={styles.errorText}>{sessionsError}</Text>
              </View>
            ) : sessions.length === 0 ? (
              <View style={styles.emptyCard}>
                <Text style={styles.emptyTitle}>Sin registros activos en DynamoDB</Text>
                <Text style={styles.emptySubtitle}>
                  Inyecta una sesión desde PowerShell para validar la ingesta SQS/Worker.
                </Text>
              </View>
            ) : (
              sessions.map((item: any) => {
                const isHard = Boolean(item.scene_config_id?.includes('HARD'));
                return (
                  <View key={item.session_id} style={styles.sessionCard}>
                    <View style={styles.sessionHeader}>
                      <Text style={styles.sessionCode}>{item.session_id}</Text>
                      <Text
                        style={[
                          styles.badge,
                          isHard ? styles.badgeHard : styles.badgeEasy,
                        ]}
                      >
                        {item.scene_config_id || 'Nivel Base'}
                      </Text>
                    </View>
                    <Text style={styles.sessionSubText}>Experiencia: {item.experiencia || 'N/A'}</Text>
                    <Text style={styles.sessionSubText}>Duración: {item.duracion_seg ?? 0} segundos</Text>

                    {item.metricas?.score !== undefined && (
                      <Text style={styles.scoreText}>
                        Puntaje Cognitivo: {item.metricas.score} pts
                      </Text>
                    )}

                    <View style={styles.sessionFooter}>
                      <Text style={styles.sessionStatus}>Participante: {item.participante_id}</Text>
                      {Boolean(item.tenant_id) && (
                        <Text style={styles.sessionDate}>Tenant: {item.tenant_id}</Text>
                      )}
                    </View>
                  </View>
                );
              })
            )}
          </View>
        )}

        {activeTab === 'Costos y Nube' && (
          <View style={styles.tabContent}>
            <Text style={styles.cardHeaderTitle}>Estructura de Costos Nube SaaS</Text>

            <View style={styles.costCard}>
              <Text style={styles.costTitle}>Suscripción Base</Text>
              <Text style={styles.costAmount}>USD $300 / mes</Text>
              <Text style={styles.costDescription}>Por institución (CESFAM, ELEAM o clínica)</Text>
            </View>

            <View style={styles.costCard}>
              <Text style={styles.costTitle}>Costo Nube por Institución</Text>
              <Text style={styles.costAmount}>USD $1.60 / mes</Text>
              <Text style={styles.costDescription}>Infraestructura Serverless en AWS</Text>
            </View>

            <View style={styles.costCard}>
              <Text style={styles.costTitle}>Costo Promedio por Sesión</Text>
              <Text style={styles.costAmount}>{systemMetrics.costoPorSesion}</Text>
              <Text style={styles.costDescription}>Meta del sistema: Máximo USD $0.02 / sesión</Text>
            </View>
          </View>
        )}
      </ScrollView>

      {/* Navbar Inferior */}
      <View style={styles.bottomNav}>
        {tabItems.map((item) => {
          const isActive = activeTab === item.key;
          return (
            <TouchableOpacity
              key={item.key}
              style={styles.navItem}
              onPress={() => setActiveTab(item.key)}
              activeOpacity={0.7}
            >
              <Ionicons
                name={isActive ? item.iconActive : item.iconInactive}
                size={22}
                color={isActive ? '#38BDF8' : '#64748B'}
              />
              <Text style={[styles.navLabel, isActive && styles.navLabelActive]}>
                {item.label}
              </Text>
            </TouchableOpacity>
          );
        })}
      </View>

      {/* Modal Desplegable de IA */}
      <Modal
        visible={aiModalVisible}
        transparent
        animationType="fade"
        onRequestClose={() => setAiModalVisible(false)}
      >
        <View style={styles.modalOverlay}>
          <View style={styles.modalContent}>
            <View style={styles.modalHeader}>
              <Text style={styles.modalTitle}>Desglose IA - {activeRole}</Text>
              <TouchableOpacity onPress={() => setAiModalVisible(false)}>
                <Ionicons name="close" size={22} color="#94A3B8" />
              </TouchableOpacity>
            </View>

            {loadingAI ? (
              <View style={styles.modalLoadingContainer}>
                <ActivityIndicator size="large" color="#38BDF8" />
                <Text style={styles.modalLoadingText}>Procesando métricas del dashboard...</Text>
              </View>
            ) : (
              <ScrollView style={styles.modalBody}>
                <Text style={styles.summaryTitle}>{currentSummary.title}</Text>

                <View style={styles.metricsBox}>
                  {currentSummary.metrics.map((item, idx) => (
                    <Text key={idx} style={styles.metricItem}>{item}</Text>
                  ))}
                </View>

                <View style={styles.insightBox}>
                  <Ionicons name="bulb-outline" size={18} color="#38BDF8" style={{ marginRight: 8 }} />
                  <Text style={styles.insightText}>{currentSummary.insight}</Text>
                </View>
              </ScrollView>
            )}
          </View>
        </View>
      </Modal>
    </SafeAreaView>
  );
}

const styles = StyleSheet.create({
  loadingContainer: {
    flex: 1,
    backgroundColor: '#0F172A',
    justifyContent: 'center',
    alignItems: 'center',
  },
  centerContent: {
    padding: 30,
    alignItems: 'center',
    justifyContent: 'center',
  },
  loadingText: {
    color: '#94A3B8',
    marginTop: 10,
    fontSize: 13,
  },
  errorBox: {
    backgroundColor: '#1E293B',
    padding: 16,
    borderRadius: 10,
    borderWidth: 1,
    borderColor: '#EF4444',
  },
  errorTitle: {
    color: '#F87171',
    fontWeight: 'bold',
    fontSize: 14,
  },
  errorText: {
    color: '#94A3B8',
    fontSize: 12,
    marginTop: 4,
  },
  emptyCard: {
    backgroundColor: '#1E293B',
    padding: 24,
    borderRadius: 10,
    alignItems: 'center',
    borderWidth: 1,
    borderColor: '#334155',
  },
  emptyTitle: {
    color: '#94A3B8',
    fontWeight: 'bold',
    fontSize: 14,
  },
  emptySubtitle: {
    color: '#64748B',
    fontSize: 12,
    textAlign: 'center',
    marginTop: 6,
  },
  scoreText: {
    color: '#4ADE80',
    fontSize: 12,
    fontWeight: '600',
    marginTop: 4,
  },
  container: {
    flex: 1,
    backgroundColor: '#0F172A',
  },
  header: {
    flexDirection: 'row',
    alignItems: 'center',
    padding: 20,
    backgroundColor: '#1E293B',
    borderBottomWidth: 1,
    borderBottomColor: '#334155',
  },
  headerTitle: {
    fontSize: 22,
    fontWeight: 'bold',
    color: '#38BDF8',
  },
  headerSubtitle: {
    fontSize: 13,
    color: '#94A3B8',
    marginTop: 2,
  },
  headerActions: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 10,
  },
  aiButton: {
    backgroundColor: '#334155',
    paddingHorizontal: 14,
    paddingVertical: 7,
    borderRadius: 8,
    borderWidth: 1,
    borderColor: '#38BDF8',
    justifyContent: 'center',
    alignItems: 'center',
  },
  aiButtonText: {
    color: '#38BDF8',
    fontWeight: 'bold',
    fontSize: 13,
  },
  logoutButton: {
    padding: 8,
    borderRadius: 8,
    backgroundColor: '#334155',
  },
  roleContainer: {
    paddingVertical: 12,
    paddingHorizontal: 16,
    backgroundColor: '#1E293B',
    borderBottomWidth: 1,
    borderBottomColor: '#334155',
  },
  sectionLabel: {
    fontSize: 12,
    color: '#64748B',
    fontWeight: '600',
    marginBottom: 6,
  },
  roleChip: {
    paddingHorizontal: 14,
    paddingVertical: 6,
    borderRadius: 20,
    backgroundColor: '#334155',
    marginRight: 8,
  },
  roleChipActive: {
    backgroundColor: '#0284C7',
  },
  roleText: {
    color: '#94A3B8',
    fontSize: 12,
    fontWeight: '500',
  },
  roleTextActive: {
    color: '#FFFFFF',
    fontWeight: 'bold',
  },
  content: {
    flex: 1,
    padding: 16,
  },
  scrollPadding: {
    paddingBottom: 20,
  },
  tabContent: {
    paddingBottom: 16,
  },
  cardHeaderTitle: {
    fontSize: 16,
    fontWeight: 'bold',
    color: '#F8FAFC',
    marginBottom: 12,
  },
  grid: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    justifyContent: 'space-between',
  },
  card: {
    width: '48%',
    backgroundColor: '#1E293B',
    padding: 14,
    borderRadius: 10,
    marginBottom: 12,
    borderWidth: 1,
    borderColor: '#334155',
  },
  cardValue: {
    fontSize: 20,
    fontWeight: 'bold',
    color: '#38BDF8',
  },
  cardLabel: {
    fontSize: 11,
    color: '#94A3B8',
    marginTop: 4,
  },
  infoBox: {
    backgroundColor: '#1E293B',
    padding: 16,
    borderRadius: 10,
    marginTop: 8,
    borderLeftWidth: 4,
    borderLeftColor: '#0EA5E9',
  },
  infoTitle: {
    fontSize: 14,
    fontWeight: 'bold',
    color: '#F8FAFC',
    marginBottom: 6,
  },
  infoDetail: {
    fontSize: 12,
    color: '#CBD5E1',
    marginTop: 2,
  },
  sessionCard: {
    backgroundColor: '#1E293B',
    padding: 14,
    borderRadius: 10,
    marginBottom: 10,
    borderWidth: 1,
    borderColor: '#334155',
  },
  sessionHeader: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
  },
  sessionCode: {
    fontSize: 15,
    fontWeight: 'bold',
    color: '#F8FAFC',
  },
  badge: {
    paddingHorizontal: 8,
    paddingVertical: 2,
    borderRadius: 12,
    fontSize: 11,
    fontWeight: 'bold',
  },
  badgeEasy: {
    backgroundColor: '#166534',
    color: '#4ADE80',
  },
  badgeHard: {
    backgroundColor: '#991B1B',
    color: '#FCA5A5',
  },
  sessionSubText: {
    fontSize: 12,
    color: '#94A3B8',
    marginTop: 4,
  },
  sessionFooter: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    marginTop: 8,
    paddingTop: 8,
    borderTopWidth: 1,
    borderTopColor: '#334155',
  },
  sessionStatus: {
    fontSize: 11,
    color: '#38BDF8',
  },
  sessionDate: {
    fontSize: 11,
    color: '#64748B',
  },
  costCard: {
    backgroundColor: '#1E293B',
    padding: 16,
    borderRadius: 10,
    marginBottom: 12,
    borderWidth: 1,
    borderColor: '#334155',
  },
  costTitle: {
    fontSize: 14,
    fontWeight: '600',
    color: '#94A3B8',
  },
  costAmount: {
    fontSize: 22,
    fontWeight: 'bold',
    color: '#22C55E',
    marginVertical: 4,
  },
  costDescription: {
    fontSize: 12,
    color: '#64748B',
  },
  bottomNav: {
    flexDirection: 'row',
    backgroundColor: '#1E293B',
    borderTopWidth: 1,
    borderTopColor: '#334155',
    paddingVertical: 8,
    paddingBottom: 12,
  },
  navItem: {
    flex: 1,
    alignItems: 'center',
    justifyContent: 'center',
  },
  navLabel: {
    fontSize: 11,
    color: '#64748B',
    marginTop: 4,
    fontWeight: '500',
  },
  navLabelActive: {
    color: '#38BDF8',
    fontWeight: 'bold',
  },
  modalOverlay: {
    flex: 1,
    backgroundColor: 'rgba(15, 23, 42, 0.85)',
    justifyContent: 'center',
    alignItems: 'center',
    padding: 20,
  },
  modalContent: {
    width: '100%',
    maxHeight: '80%',
    backgroundColor: '#1E293B',
    borderRadius: 16,
    borderWidth: 1,
    borderColor: '#38BDF8',
    padding: 20,
  },
  modalHeader: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: 16,
  },
  modalTitle: {
    color: '#FFFFFF',
    fontSize: 16,
    fontWeight: 'bold',
  },
  modalLoadingContainer: {
    padding: 40,
    alignItems: 'center',
    gap: 12,
  },
  modalLoadingText: {
    color: '#94A3B8',
    fontSize: 14,
  },
  modalBody: {
    marginTop: 4,
  },
  summaryTitle: {
    color: '#38BDF8',
    fontSize: 15,
    fontWeight: 'bold',
    marginBottom: 12,
  },
  metricsBox: {
    backgroundColor: '#0F172A',
    padding: 14,
    borderRadius: 10,
    gap: 10,
    marginBottom: 16,
  },
  metricItem: {
    color: '#CBD5E1',
    fontSize: 13,
    lineHeight: 18,
  },
  insightBox: {
    flexDirection: 'row',
    backgroundColor: '#0369A120',
    borderWidth: 1,
    borderColor: '#0284C7',
    padding: 12,
    borderRadius: 10,
    alignItems: 'center',
  },
  insightText: {
    color: '#E0F2FE',
    fontSize: 13,
    flex: 1,
    lineHeight: 18,
  },
});