const ACCESS = "clyiq_access";
const REFRESH = "clyiq_refresh";
const REMEMBER = "clyiq_remember";

function getStore() {
  const remember = localStorage.getItem(REMEMBER) === "1";
  return remember ? localStorage : sessionStorage;
}

export const tokenStore = {
  set(accessToken: string, refreshToken: string, remember: boolean) {
    // clear both
    localStorage.removeItem(ACCESS); localStorage.removeItem(REFRESH);
    sessionStorage.removeItem(ACCESS); sessionStorage.removeItem(REFRESH);

    const store = remember ? localStorage : sessionStorage;
    store.setItem(ACCESS, accessToken);
    store.setItem(REFRESH, refreshToken);
    localStorage.setItem(REMEMBER, remember ? "1" : "0");
  },

  getAccess() {
    return getStore().getItem(ACCESS);
  },

  getRefresh() {
    return getStore().getItem(REFRESH);
  },

  clear() {
    localStorage.removeItem(ACCESS);
    localStorage.removeItem(REFRESH);
    localStorage.removeItem(REMEMBER);
    sessionStorage.removeItem(ACCESS);
    sessionStorage.removeItem(REFRESH);
  }
};