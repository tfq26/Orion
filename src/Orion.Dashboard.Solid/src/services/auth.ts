export interface User {
  isAuthenticated: boolean;
  userId?: string;
  email?: string;
  name?: string;
  picture?: string;
}

declare global {
  interface Window {
    __TAURI_INTERNALS__?: unknown;
  }
}

function isDesktopShell() {
  return typeof window !== 'undefined' && typeof window.__TAURI_INTERNALS__ !== 'undefined';
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

  async login() {
    if (!isDesktopShell()) {
      window.location.href = '/auth/login';
      return;
    }

    const [{ openUrl }, { getCurrent, onOpenUrl }] = await Promise.all([
      import('@tauri-apps/plugin-opener'),
      import('@tauri-apps/plugin-deep-link')
    ]);

    const authResponse = await fetch('/auth/login-url?source=desktop');
    if (!authResponse.ok) {
      throw new Error('Unable to start desktop sign-in.');
    }

    const { url } = await authResponse.json();

    const exchangeCode = async (callbackUrl: string) => {
      const parsed = new URL(callbackUrl);
      const code = parsed.searchParams.get('code');
      if (!code) {
        throw new Error('No authentication code returned from WorkOS.');
      }

      const exchangeResponse = await fetch('/auth/exchange', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json'
        },
        credentials: 'include',
        body: JSON.stringify({
          code,
          source: 'desktop'
        })
      });

      if (!exchangeResponse.ok) {
        const error = await exchangeResponse.text();
        throw new Error(error || 'Desktop sign-in failed.');
      }

      window.location.href = '/';
    };

    const current = await getCurrent();
    if (Array.isArray(current) && current.length > 0) {
      await exchangeCode(current[0]);
      return;
    }

    const unlisten = await onOpenUrl(async (urls) => {
      const firstUrl = urls?.[0];
      if (!firstUrl) {
        return;
      }

      unlisten();
      await exchangeCode(firstUrl);
    });

    await openUrl(url);
  },

  logout() {
    window.location.href = '/auth/logout';
  }
};
