// main.ts - Start mockup B (Svelte, hand-made parts)

import { mount } from "svelte";
import App from "./App.svelte";
import "../shared/layout.css";
import "../shared/keypad.css";

mount(App, { target: document.getElementById("app")! });
