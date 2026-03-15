/** @type {import('tailwindcss').Config} */
module.exports = {
  content: [
    "./index.html",
    "./src/**/*.{js,ts,jsx,tsx}",
  ],
  theme: {
    extend: {
      colors: {
        'background': '#000000',
        'surface': '#0A0A0A',
        'surface-elevated': '#121212',
        'accent-cyan': '#00E6F3',
        'accent-purple': '#BC6FF1',
      },
      fontFamily: {
        'sans': ['Outfit', 'sans-serif'],
      },
      boxShadow: {
        'glow-cyan': '0 0 20px rgba(0, 230, 243, 0.2)',
        'glow-purple': '0 0 20px rgba(188, 111, 241, 0.2)',
      }
    },
  },
  plugins: [],
}
