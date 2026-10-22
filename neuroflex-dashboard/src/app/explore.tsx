import { useState } from 'react';
import { ActivityIndicator, FlatList, Platform, RefreshControl, StyleSheet, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { ThemedText } from '@/components/themed-text';
import { ThemedView } from '@/components/themed-view';
import { BottomTabInset, MaxContentWidth, Spacing } from '@/constants/theme';
import { useTheme } from '@/hooks/use-theme';
import { useSessions } from '../hooks/useSessions';

export default function TabTwoScreen() {
  const safeAreaInsets = useSafeAreaInsets();
  const insets = {
    ...safeAreaInsets,
    bottom: safeAreaInsets.bottom + BottomTabInset + Spacing.three,
  };
  const theme = useTheme();

  const [refreshing, setRefreshing] = useState(false);
  const { sessions, loading, error, refetch } = useSessions('P001');

  const onRefresh = async () => {
    setRefreshing(true);
    if (refetch) {
      await refetch();
    }
    setRefreshing(false);
  };

  const contentPlatformStyle = Platform.select({
    android: {
      paddingTop: insets.top,
      paddingLeft: insets.left,
      paddingRight: insets.right,
      paddingBottom: insets.bottom,
    },
    web: {
      paddingTop: Spacing.six,
      paddingBottom: Spacing.four,
    },
  });

  if (loading && !refreshing) {
    return (
      <ThemedView style={[styles.scrollView, styles.centerContent, { backgroundColor: theme.background }]}>
        <ActivityIndicator size="large" color="#0284c7" />
        <ThemedText style={{ marginTop: Spacing.two }} themeColor="textSecondary">
          Consultando registros reales en AWS DynamoDB...
        </ThemedText>
      </ThemedView>
    );
  }

  if (error && !refreshing) {
    return (
      <ThemedView style={[styles.scrollView, styles.centerContent, { backgroundColor: theme.background }]}>
        <ThemedText type="subtitle" style={{ color: '#f87171' }}>
          Error al consultar la API de AWS
        </ThemedText>
        <ThemedText style={{ marginTop: Spacing.one, textAlign: 'center' }} themeColor="textSecondary">
          {error}
        </ThemedText>
      </ThemedView>
    );
  }

  return (
    <ThemedView style={[styles.scrollView, { backgroundColor: theme.background }]}>
      <ThemedView style={[styles.container, contentPlatformStyle]}>
        <ThemedView style={styles.titleContainer}>
          <ThemedText type="subtitle">Telemetría de Sesiones VR</ThemedText>
          <ThemedText style={styles.centerText} themeColor="textSecondary">
            Silo AWS: Participante P001 • {sessions.length} registro(s) en caliente
          </ThemedText>
        </ThemedView>

        <FlatList
          data={sessions}
          keyExtractor={(item: any) => item.session_id}
          contentContainerStyle={styles.listContainer}
          refreshControl={
            <RefreshControl refreshing={refreshing} onRefresh={onRefresh} tintColor="#0284c7" />
          }
          ListEmptyComponent={
            <ThemedView type="backgroundElement" style={styles.emptyCard}>
              <ThemedText style={styles.emptyCardTitle}>
                Sin registros activos en DynamoDB
              </ThemedText>
              <ThemedText style={styles.emptyCardText}>
                Inyecta una sesión desde PowerShell para validar la ingesta SQS/Worker.
              </ThemedText>
            </ThemedView>
          }
          renderItem={({ item }: { item: any }) => {
            const isHard = item.scene_config_id?.includes('HARD');
            return (
              <ThemedView type="backgroundElement" style={styles.card}>
                <View style={styles.cardHeader}>
                  <ThemedText style={styles.cardTitle}>
                    {item.session_id}
                  </ThemedText>
                  <ThemedText style={[styles.badge, { color: isHard ? '#f87171' : '#4ade80' }]}>
                    {item.scene_config_id || 'Nivel Base'}
                  </ThemedText>
                </View>

                <ThemedText style={styles.regularText}>Experiencia: {item.experiencia || 'N/A'}</ThemedText>
                <ThemedText style={styles.regularText}>Duración: {item.duracion_seg ?? 0} seg</ThemedText>

                <View style={styles.metricsRow}>
                  {item.metricas?.score !== undefined && (
                    <ThemedText style={styles.scoreText}>
                      Puntaje: {item.metricas.score} pts
                    </ThemedText>
                  )}
                  {item.metricas?.avg_hr !== undefined && (
                    <ThemedText style={styles.hrText}>
                      FC Promedio: {item.metricas.avg_hr} bpm
                    </ThemedText>
                  )}
                </View>

                {item.tenant_id && (
                  <ThemedText style={styles.tenantFooter}>
                    Tenant: {item.tenant_id}
                  </ThemedText>
                )}
              </ThemedView>
            );
          }}
        />
      </ThemedView>
    </ThemedView>
  );
}

const styles = StyleSheet.create({
  scrollView: {
    flex: 1,
  },
  centerContent: {
    justifyContent: 'center',
    alignItems: 'center',
    padding: Spacing.four,
  },
  container: {
    maxWidth: MaxContentWidth,
    flexGrow: 1,
    alignSelf: 'center',
    width: '100%',
  },
  titleContainer: {
    gap: Spacing.one,
    alignItems: 'center',
    paddingHorizontal: Spacing.four,
    paddingVertical: Spacing.four,
  },
  centerText: {
    textAlign: 'center',
  },
  listContainer: {
    paddingHorizontal: Spacing.four,
    paddingBottom: Spacing.six,
    gap: Spacing.three,
  },
  card: {
    padding: Spacing.three,
    borderRadius: Spacing.two,
    gap: Spacing.one,
  },
  cardHeader: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: Spacing.one,
  },
  cardTitle: {
    color: '#38bdf8',
    fontWeight: 'bold',
    fontSize: 14,
  },
  badge: {
    fontSize: 12,
    fontWeight: 'bold',
  },
  regularText: {
    fontSize: 14,
  },
  metricsRow: {
    flexDirection: 'row',
    gap: Spacing.three,
    marginTop: Spacing.one,
    alignItems: 'center',
  },
  scoreText: {
    color: '#4ade80',
    fontWeight: 'bold',
    fontSize: 14,
  },
  hrText: {
    color: '#fbbf24',
    fontSize: 14,
  },
  tenantFooter: {
    color: '#64748b',
    fontSize: 11,
    marginTop: Spacing.one,
  },
  emptyCard: {
    padding: Spacing.five,
    borderRadius: Spacing.two,
    alignItems: 'center',
    marginTop: Spacing.four,
  },
  emptyCardTitle: {
    color: '#94a3b8',
    textAlign: 'center',
    fontWeight: 'bold',
    fontSize: 14,
  },
  emptyCardText: {
    color: '#64748b',
    textAlign: 'center',
    marginTop: Spacing.one,
    fontSize: 14,
  },
});