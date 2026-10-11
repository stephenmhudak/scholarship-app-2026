import api from '../services/api'

// Must match FileUploadLimits.MaxFileSizeBytes on the backend
export const MAX_FILE_SIZE = 5 * 1024 * 1024

export const ACCEPTED_REFERENCE_TYPES = ['.jpg', '.jpeg', '.doc', '.docx', '.pdf', '.png']

/**
 * Upload a file to the given endpoint as multipart/form-data.
 * Returns the fileId from the response.
 *
 * @param {string} endpoint - API endpoint path
 * @param {File} file - File object to upload
 * @returns {Promise<string>} - resolved fileId
 */
export async function uploadFile(endpoint, file) {
  const formData = new FormData()
  formData.append('file', file)

  const response = await api.post(endpoint, formData, {
    headers: {
      'Content-Type': 'multipart/form-data',
    },
  })

  return response.data.fileId
}
