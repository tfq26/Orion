export interface User {
  isAuthenticated: boolean;
  userId?: string;
  email?: string;
  name?: string;
  picture?: string;
}

export const authService = {
  async getUser(): Promise<User> {
    try {
      const res = await fetch('/auth/user');
      if (!res.ok) return { isAuthenticated: false };
      return await res.json();
    } catch (e) {
      console.error('[AUTH] Failed to fetch user', e);
      return { isAuthenticated: false };
    }
  },

  login() {
    window.location.href = '/auth/login';
  },

  logout() {
    window.location.href = '/auth/logout';
  }
};
