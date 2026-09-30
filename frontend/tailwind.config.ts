import type { Config } from "tailwindcss";

export default {
  content: ["./app/**/*.{ts,tsx}", "./components/**/*.{ts,tsx}"],
  theme: {
    extend: {
      colors: {
        ink: {
          DEFAULT: "#172033",
          light: "#334155",
          muted: "#64748b",
        },
        brand: {
          DEFAULT: "#0B6E69",
          50: "#f0fdfa",
          100: "#ccfbf1",
          500: "#14b8a6",
          600: "#0d9488",
          700: "#0B6E69",
          800: "#115e59",
          900: "#134e4a",
        },
        accent: {
          DEFAULT: "#b45309",
          50: "#fffbeb",
          100: "#fef3c7",
          500: "#d97706",
          600: "#b45309",
          700: "#92400e",
          800: "#78350f",
        },
        surface: {
          DEFAULT: "#F7F8FA",
          card: "#ffffff",
          muted: "#f1f5f9",
        },
      },
      fontFamily: {
        sans: [
          "system-ui",
          "-apple-system",
          "BlinkMacSystemFont",
          "Segoe UI",
          "Roboto",
          "sans-serif",
        ],
      },
      screens: {
        catalog: "1300px",
        wide: "1600px",
      },
    },
  },
  plugins: [],
} satisfies Config;
