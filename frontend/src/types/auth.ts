import type { DemoUser } from "../App";

export type AuthSession = {
  accessToken: string;
  refreshToken: string;
  expiresAt: string;
  user: DemoUser;
};

export type ChangePasswordInput = {
  currentPassword: string;
  newPassword: string;
  confirmPassword: string;
};
