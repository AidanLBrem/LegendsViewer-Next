import { createApp } from 'vue'
import { isRouteAssetLoadError, retryRouteAssetOnce, ROUTE_ASSET_RETRY_KEY } from './utils/routeAssetRecovery'
import './style.css'

// Components
import App from './App.vue'

// Vue Router
import router from './router'
// Pinia
import { createPinia } from 'pinia'
const pinia = createPinia()

// Vuetify
import 'vuetify/styles'
import '@mdi/font/css/materialdesignicons.css' // Ensure you are using css-loader
import { createVuetify } from 'vuetify'
import { aliases, mdi } from 'vuetify/iconsets/mdi'
import * as components from 'vuetify/components'
import * as directives from 'vuetify/directives'

const vuetify = createVuetify({
  components,
  directives,
  icons: {
    defaultSet: 'mdi',
    aliases,
    sets: {
      mdi,
    },
  },
  theme: {
    defaultTheme: 'dark'
  }
})

// A tab left open during a rebuild can still reference old hashed route
// chunks. Handle the failure in the UI, without changing the build or server.
function reloadFailedRouteAsset(error: unknown): void {
  if (!isRouteAssetLoadError(error)) return;
  if (!retryRouteAssetOnce(window.sessionStorage, () => window.location.reload())) {
    console.error('The route module is still unavailable after one page reload.', error);
  }
}

// Vite dispatches this event for failed JS or CSS preloads.
window.addEventListener('vite:preloadError', (event: Event) => {
  event.preventDefault();
  const failure = event as Event & { payload?: unknown };
  if (!retryRouteAssetOnce(window.sessionStorage, () => window.location.reload())) {
    console.error('The route preload is still unavailable after one page reload.', failure.payload);
  }
});

// Vue Router can also report a failed dynamic import before/without preload.
router.onError(reloadFailedRouteAsset);

// Clear the retry guard only after a route has loaded successfully.
// A failed initial navigation leaves it in place, avoiding reload loops.
router.isReady().then(() => {
  window.sessionStorage.removeItem(ROUTE_ASSET_RETRY_KEY);
}).catch(() => { /* Keep guard if the initial route never loaded. */ });

createApp(App)
  .use(router)
  .use(pinia)
  .use(vuetify)
  .mount('#app')