import type { Theme } from "vitepress";
import DefaultTheme from "vitepress/theme";
import HomePage from "./HomePage.vue";
import "./custom.css";

export default {
  extends: DefaultTheme,
  enhanceApp({ app }) {
    app.component("HomePage", HomePage);
  }
} satisfies Theme;
