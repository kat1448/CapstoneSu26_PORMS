/** @type {import('tailwindcss').Config} */
module.exports = {
  content: ["./index.html", "./src/**/*.{ts,tsx}"],
  theme: {
    extend: {
      colors: {
        porms: {
          amber: "#e9a11b",
          bg: "#eef3f8",
          blue: "#2f6fab",
          cyan: "#1594b8",
          green: "#19a66a",
          line: "#d8e2ec",
          muted: "#6b7c8d",
          navy: "#10283d",
          orange: "#ee7623",
          red: "#d94848",
          text: "#162534"
        }
      },
      boxShadow: {
        porms: "0 8px 28px rgba(16,40,61,.09)"
      },
      fontFamily: {
        sans: ["Inter", "Segoe UI", "Arial", "sans-serif"]
      }
    }
  },
  plugins: []
};
