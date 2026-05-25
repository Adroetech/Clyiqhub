import axios from "axios";
import { tokenStore } from "./tokenStore";



const api = axios.create({
  baseURL: "",
  timeout: 10000, // ✅ 10 seconds so it never spins forever
  headers: { "Content-Type": "application/json" }
});


api.interceptors.request.use((config) => {
  const url = config.url || "";

  // ✅ Don't send Authorization header for login/refresh/logout/forgot/set-password
  if (url.startsWith("/api/auth/")) return config;

  const token = tokenStore.getAccess();
  if (token) config.headers.Authorization = `Bearer ${token}`;
  return config;
});


export default api;
