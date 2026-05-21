import defaultTheme from 'tailwindcss/defaultTheme'

export default {
  content: ['./index.html', './src/**/*.{vue,js}'],
  theme: {
    extend: {
      fontFamily: {
        sans: ['Inter', ...defaultTheme.fontFamily.sans],
      },
      colors: {
        primary: {
          50:  '#EEF2FF',
          100: '#E0E7FF',
          200: '#C7D2FE',
          300: '#A5B4FC',
          400: '#818CF8',
          500: '#6366F1',
          600: '#4361EE',
          700: '#3451CB',
          800: '#2940A8',
          900: '#1E2F85',
          DEFAULT: '#4361EE',
        },
        blue: {
          50:  '#EEF2FF',
          100: '#E0E7FF',
          200: '#C7D2FE',
          300: '#A5B4FC',
          400: '#818CF8',
          500: '#6366F1',
          600: '#4361EE',
          700: '#3451CB',
          800: '#2940A8',
          900: '#1E2F85',
        },
        navy: {
          DEFAULT: '#2B3674',
          50:  '#F0F3FA',
          100: '#D8DFF0',
          200: '#B2C0E0',
          300: '#8CA1D1',
          400: '#6882C1',
          500: '#4463B2',
          600: '#2B3674',
          700: '#1F2857',
          800: '#141A3A',
          900: '#0A0D1D',
        },
        success: {
          DEFAULT: '#01B574',
          light:   '#E6FAF5',
          dark:    '#018F5C',
        },
        danger: {
          DEFAULT: '#EE5D50',
          light:   '#FFF5F5',
          dark:    '#C94A3E',
        },
        warning: {
          DEFAULT: '#FFB547',
          light:   '#FFF8EB',
          dark:    '#CC8F32',
        },
      },
      backgroundColor: {
        page: '#F4F7FE',
      },
      boxShadow: {
        card:      '0 2px 12px 0 rgba(67, 97, 238, 0.06)',
        'card-md': '0 4px 20px 0 rgba(67, 97, 238, 0.10)',
        'card-lg': '0 8px 32px 0 rgba(67, 97, 238, 0.14)',
      },
    },
  },
  plugins: [],
}
