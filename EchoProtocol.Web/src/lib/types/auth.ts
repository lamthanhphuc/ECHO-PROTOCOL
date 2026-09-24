import type { UserRole } from "./common";

export interface LoginRequest {
  username: string;
  password: string;
}

export interface AuthUser {
  id: string;
  email?: string;
  username: string;
  role: UserRole;
}

export interface AuthResponse {
  accessToken: string;
  expiresAt: string;
  user: AuthUser;
  wallet: { balance: number };
}

export interface SessionUser {
  userId: string;
  username: string;
  role: UserRole;
  expiresAt: number;
}
