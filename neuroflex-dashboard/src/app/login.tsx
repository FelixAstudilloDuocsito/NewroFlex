import AsyncStorage from '@react-native-async-storage/async-storage';
import { router } from 'expo-router';
import { useState } from 'react';
import {
  ActivityIndicator,
  Alert,
  SafeAreaView,
  StatusBar,
  StyleSheet,
  Text,
  TextInput,
  TouchableOpacity,
  View,
} from 'react-native';
import Svg, { Circle, Path, Rect } from 'react-native-svg';

const COGNITO_REGION = 'us-east-1';
const COGNITO_CLIENT_ID = '1eto1qkukfh681a7v9bic56mka';

export default function LoginScreen() {
  const [email, setEmail] = useState('especialista@haba.test');
  const [password, setPassword] = useState('NeuroFlex2026!');
  const [loading, setLoading] = useState(false);

  const handleLogin = async () => {
    if (!email.trim() || !password.trim()) {
      Alert.alert('Error', 'Por favor ingresa tu correo y contraseña');
      return;
    }

    setLoading(true);

    try {
      const response = await fetch(`https://cognito-idp.${COGNITO_REGION}.amazonaws.com/`, {
        method: 'POST',
        headers: {
          'X-Amz-Target': 'AWSCognitoIdentityProviderService.InitiateAuth',
          'Content-Type': 'application/x-amz-json-1.1',
        },
        body: JSON.stringify({
          AuthFlow: 'USER_PASSWORD_AUTH',
          ClientId: COGNITO_CLIENT_ID,
          AuthParameters: {
            USERNAME: email.trim(),
            PASSWORD: password,
          },
        }),
      });

      const data = await response.json();

      if (!response.ok) {
        const errorMsg = data.message || 'Credenciales incorrectas.';
        Alert.alert('Error de Autenticación', errorMsg);
        return;
      }

      // Usar IdToken (requerido por API Gateway Cognito Authorizer por defecto)
      const idToken = data.AuthenticationResult?.IdToken;
// Forzar estrictamente el uso del Access Token
      const accessToken = data.AuthenticationResult?.AccessToken;

      if (!accessToken) {
        Alert.alert('Error', 'No se obtuvo un Access Token válido de Cognito.');
        return;
      }

      const userSession = {
        email: email.trim(),
        role: 'Especialista',
        token: accessToken, // <-- Se envía el token correcto a la API
        isLoggedIn: true,
      };

      await AsyncStorage.setItem('user_session', JSON.stringify(userSession));
      router.replace('/');
    } catch (error: any) {
      Alert.alert('Error de Conexión', error.message || 'No se pudo conectar con Cognito');
    } finally {
      setLoading(false);
    }
  };

  return (
    <SafeAreaView style={styles.container}>
      <StatusBar barStyle="light-content" backgroundColor="#0F172A" />

      <View style={styles.content}>
        <View style={styles.header}>
          <View style={styles.iconContainer}>
            <Svg viewBox="0 0 120 120" width={70} height={70}>
              <Circle cx="60" cy="60" r="56" fill="#1E293B" stroke="#38BDF8" strokeWidth="2" />
              <Rect x="25" y="45" width="70" height="30" rx="12" fill="#0F172A" stroke="#38BDF8" strokeWidth="4" />
              <Path d="M 40 60 Q 60 40 80 60" fill="none" stroke="#0284C7" strokeWidth="4" strokeLinecap="round" />
              <Circle cx="45" cy="60" r="4" fill="#38BDF8" />
              <Circle cx="75" cy="60" r="4" fill="#38BDF8" />
            </Svg>
          </View>
          <Text style={styles.title}>NeuroFlex VR</Text>
          <Text style={styles.subtitle}>Acceso al Panel SaaS Institucional</Text>
        </View>

        <View style={styles.form}>
          <Text style={styles.label}>Correo Electrónico</Text>
          <TextInput
            style={styles.input}
            placeholder="especialista@haba.test"
            placeholderTextColor="#64748B"
            value={email}
            onChangeText={setEmail}
            keyboardType="email-address"
            autoCapitalize="none"
          />

          <Text style={styles.label}>Contraseña</Text>
          <TextInput
            style={styles.input}
            placeholder="••••••••"
            placeholderTextColor="#64748B"
            value={password}
            onChangeText={setPassword}
            secureTextEntry
          />

          <TouchableOpacity style={styles.button} onPress={handleLogin} disabled={loading}>
            {loading ? <ActivityIndicator color="#FFFFFF" /> : <Text style={styles.buttonText}>Iniciar Sesión</Text>}
          </TouchableOpacity>
        </View>
      </View>
    </SafeAreaView>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1, backgroundColor: '#0F172A' },
  content: { flex: 1, justifyContent: 'center', paddingHorizontal: 24 },
  header: { alignItems: 'center', marginBottom: 32 },
  iconContainer: { alignItems: 'center', justifyContent: 'center', marginBottom: 16 },
  title: { fontSize: 28, fontWeight: 'bold', color: '#38BDF8' },
  subtitle: { fontSize: 14, color: '#94A3B8', marginTop: 4 },
  form: { backgroundColor: '#1E293B', padding: 20, borderRadius: 12, borderWidth: 1, borderColor: '#334155' },
  label: { fontSize: 12, color: '#CBD5E1', fontWeight: '600', marginBottom: 6, marginTop: 10 },
  input: { backgroundColor: '#0F172A', color: '#F8FAFC', borderRadius: 8, paddingHorizontal: 12, paddingVertical: 10, borderWidth: 1, borderColor: '#334155', fontSize: 14 },
  button: { backgroundColor: '#0284C7', paddingVertical: 12, borderRadius: 8, alignItems: 'center', marginTop: 20 },
  buttonText: { color: '#FFFFFF', fontWeight: 'bold', fontSize: 15 },
});