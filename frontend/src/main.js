// Styles first: layers.css fixes the cascade-layer order, and must load before any
// Vuetify component styles pulled in by the router/App imports below.
import './assets/layers.css'
import '@mdi/font/css/materialdesignicons.css'
import 'vuetify/styles'
import './assets/main.css'

import { createApp } from 'vue'
import { createPinia } from 'pinia'
import router from './router'
import vuetify from './plugins/vuetify'
import App from './App.vue'

createApp(App).use(createPinia()).use(router).use(vuetify).mount('#app')
