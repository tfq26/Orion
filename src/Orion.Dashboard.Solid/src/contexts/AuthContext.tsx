import { createContext, createSignal, useContext, JSX, onMount, createResource } from 'solid-js';
import { authService, User } from '../services/auth';

interface AuthContextType {
  user: () => User | undefined;
  loading: () => boolean;
  login: () => void;
  logout: () => void;
  recheck: () => void;
}

const AuthContext = createContext<AuthContextType>();

export function AuthProvider(props: { children: JSX.Element }) {
  const [user, { refetch }] = createResource(authService.getUser);

  const login = () => authService.login();
  const logout = () => authService.logout();
  const recheck = () => refetch();

  return (
    <AuthContext.Provider value={{ 
      user, 
      loading: () => user.loading,
      login, 
      logout,
      recheck
    }}>
      {props.children}
    </AuthContext.Provider>
  );
}

export function useAuth() {
  const context = useContext(AuthContext);
  if (!context) throw new Error('useAuth must be used within an AuthProvider');
  return context;
}
