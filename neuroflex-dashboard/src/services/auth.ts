export async function loginCognito(username: string, password: string): Promise<string> {
  const response = await fetch('https://cognito-idp.us-east-1.amazonaws.com/', {
    method: 'POST',
    headers: {
      'X-Amz-Target': 'AWSCognitoIdentityProviderService.InitiateAuth',
      'Content-Type': 'application/x-amz-json-1.1',
    },
    body: JSON.stringify({
      AuthFlow: 'USER_PASSWORD_AUTH',
      ClientId: '1eto1qkukfh681a7v9bic56mka',
      AuthParameters: {
        USERNAME: username,
        PASSWORD: password,
      },
    }),
  });

  const data = await response.json();
  if (!response.ok) {
    throw new Error(data.message || 'Credenciales inválidas');
  }

  return data.AuthenticationResult.AccessToken;
}