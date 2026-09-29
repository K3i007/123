import type { Config } from "tailwindcss";
export default {
  content: ["./app/**/*.{ts,tsx}", "./components/**/*.{ts,tsx}"],
  theme: {
    extend: {
      colors: { ink: "#172033", brand: "#0B6E69", accent: "#E0A458", surface: "#F7F8FA" },
      fontFamily: { sans: ["Arial", "sans-serif"] },
      screens: { catalog: "1300px", wide: "1600px" },
    },
  },
  plugins: [],
} satisfies Config;
