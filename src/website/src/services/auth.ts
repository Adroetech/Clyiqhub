import api from "./api";
import { tokenStore } from "./tokenStore";


export async function loginApi(email: string, password: string, remember: boolean) {
  const payload = { email, password };   // ✅ use camelCase
  const res = await api.post("/api/auth/login", payload);

  const { accessToken, refreshToken } = res.data;
  tokenStore.set(accessToken, refreshToken, remember);
}


export async function logoutApi() {
  const refreshToken = tokenStore.getRefresh();
  try {
    if (refreshToken) {
      await api.post("/api/auth/logout", { refreshToken });
    }
  } finally {
    tokenStore.clear();
  }
}