// Development build (ng serve / ng build --configuration development).
// '/api' is forwarded to the backend by proxy.conf.json, so the app and the API share one origin
// (as in production) and the SameSite=Strict refresh-token cookie is sent.
export const environment = {
  production: false,
  apiBaseUrl: '/api',
};
