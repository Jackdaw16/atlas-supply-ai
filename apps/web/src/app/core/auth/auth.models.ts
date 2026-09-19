export interface LoginRequest {
  username: string;
  password: string;
}

export interface AuthSession {
  accessToken: string;
  expiresAtUtc: string;
  username: string;
  scopes: string[];
}
