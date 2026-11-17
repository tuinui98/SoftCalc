// main.ts - Start mockup A (Ionic + Vue) in Android (md) style on every device

import { createApp } from "vue";
import { IonicVue } from "@ionic/vue";
import App from "./App.vue";

import "@ionic/vue/css/core.css";
import "@ionic/vue/css/normalize.css";
import "@ionic/vue/css/structure.css";
import "@ionic/vue/css/typography.css";
import "@ionic/vue/css/palettes/dark.class.css";
import "../shared/layout.css";
import "../shared/keypad.css";

createApp(App).use(IonicVue, { mode: "md" }).mount("#app");
