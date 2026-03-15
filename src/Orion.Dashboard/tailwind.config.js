/** @type {import('tailwindcss').Config} */
module.exports = {
  content: [
    "./**/*.{razor,html,cshtml}",
    "../**/*.{razor,html,cshtml}"
  ],
  theme: {
    extend: {
      colors: {
        background: '#000000',
        surface: '#050505',
        'surface-elevated': '#0a0a0a',
        border: '#161616',
        primary: '#f8fafc',
        secondary: '#94a3b8',
        accent: {
          cyan: '#00e6f3',
        },
        success: '#34D59A',
        warning: '#ffb800',
        error: '#ff3366'
      },
      fontFamily: {
        sans: ['Inter', 'sans-serif'],
        mono: ['JetBrains Mono', 'monospace'],
      }
    },
  },
  plugins: [],
}
