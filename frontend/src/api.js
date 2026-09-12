import axios from 'axios';

const API_BASE = import.meta.env.VITE_API_BASE || 'http://localhost:5208';

const api = axios.create({
  baseURL: API_BASE,
  timeout: 60000,
});

export const uploadDocument = (file, onProgress) => {
  const formData = new FormData();
  formData.append('file', file);
  return api.post('/api/documents/upload', formData, {
    headers: { 'Content-Type': 'multipart/form-data' },
    onUploadProgress: (e) => {
      if (onProgress) onProgress(Math.round((e.loaded * 100) / e.total));
    },
  });
};

export const getDocuments = () => api.get('/api/documents');

export const deleteDocument = (id) => api.delete(`/api/documents/${id}`);

export const sendChat = (question) =>
  api.post('/api/chat', { question });

export const getChatHistory = () => api.get('/api/chat/history');

export const clearChatHistory = () => api.delete('/api/chat/history');
