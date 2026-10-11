import { createVuetify } from 'vuetify'
import { aliases, mdi } from 'vuetify/iconsets/mdi'

// Dialect Design System palette. Status colors use the darker shades so they stay
// readable as text in tonal alerts and chips.
const dialect = {
  dark: false,
  colors: {
    background: '#F4F7FE',
    surface: '#FFFFFF',
    'surface-light': '#F4F7FE',
    'on-background': '#2B3674',
    'on-surface': '#2B3674',
    primary: '#4361EE',
    secondary: '#2B3674',
    info: '#4361EE',
    success: '#018F5C',
    warning: '#8C6500',
    error: '#C94A3E',
  },
  variables: {
    'border-color': '#E9EDF7',
    'border-opacity': 1,
  },
}

export default createVuetify({
  theme: {
    defaultTheme: 'dialect',
    themes: { dialect },
  },
  icons: {
    defaultSet: 'mdi',
    aliases,
    sets: { mdi },
  },
  defaults: {
    VAlert: { variant: 'tonal', border: 'start', density: 'comfortable' },
    VAutocomplete: { variant: 'outlined', density: 'comfortable', color: 'primary' },
    VBtn: { rounded: 'lg', variant: 'flat', class: 'text-none' },
    VCard: { rounded: 'xl', variant: 'flat', border: true, class: 'shadow-card' },
    VCheckbox: { color: 'primary', density: 'compact', hideDetails: 'auto' },
    VChip: { size: 'small', variant: 'tonal', label: false },
    VDataTable: { hover: true, itemsPerPage: 25 },
    VDataTableServer: { hover: true },
    VDateInput: { variant: 'outlined', density: 'comfortable', color: 'primary', prependIcon: '', prependInnerIcon: '$calendar' },
    VDialog: { maxWidth: 512 },
    VFileInput: { variant: 'outlined', density: 'comfortable', color: 'primary', prependIcon: '', prependInnerIcon: '$file' },
    VNumberInput: { variant: 'outlined', density: 'comfortable', color: 'primary' },
    VRadioGroup: { color: 'primary', density: 'compact', hideDetails: 'auto' },
    VSelect: { variant: 'outlined', density: 'comfortable', color: 'primary' },
    VSwitch: { color: 'primary', inset: true, density: 'compact', hideDetails: 'auto' },
    VTextField: { variant: 'outlined', density: 'comfortable', color: 'primary' },
    VTextarea: { variant: 'outlined', density: 'comfortable', color: 'primary', autoGrow: true },
  },
})
