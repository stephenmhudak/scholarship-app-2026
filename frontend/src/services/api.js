import axios from 'axios'
import router from '../router'

const api = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL || '/api',
  headers: {
    'Content-Type': 'application/json',
  },
})

// Request interceptor — attach Bearer token from localStorage
api.interceptors.request.use(
  (config) => {
    const token = localStorage.getItem('auth_token')
    if (token) {
      config.headers.Authorization = `Bearer ${token}`
    }
    return config
  },
  (error) => Promise.reject(error)
)

// Response interceptor — keep a replacement token, redirect to /login on 401
api.interceptors.response.use(
  (response) => {
    // Password and two-factor changes end every other session; the API hands this one a new token
    const token = response.headers['x-session-token']
    if (token) localStorage.setItem('auth_token', token)
    return response
  },
  (error) => {
    if (error.response?.status === 401) {
      localStorage.removeItem('auth_token')
      localStorage.removeItem('impersonator_name')
      router.push('/login')
    }
    return Promise.reject(error)
  }
)

export default api
