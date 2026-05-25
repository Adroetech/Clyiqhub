import axios from "axios";
import { tokenStore } from "./tokenStore";

const api = axios.create({
    baseURL: import.meta.env.VITE_API_BASE,
    headers: { "Content-Type": "application/json" }
});

// attach Bearer token automatically
api.interceptors.request.use((config) => {
    const token = tokenStore.getAccess();
    if (token) config.headers.Authorization = `Bearer ${token}`;
    return config;
});

export default api;