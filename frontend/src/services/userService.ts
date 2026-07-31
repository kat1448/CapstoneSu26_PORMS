import {
  createUser as createDemoUser,
  deleteUser as deleteDemoUser,
  getUsers as getUsersData,
  updateUser as updateDemoUser
} from "../mock/demoData";
import { requestJson, requestVoid, withMockFallback } from "./api";

export type UserRole = "ADMIN" | "PORT_MANAGER" | "OPERATOR";
export type UserStatus = "ACTIVE" | "INACTIVE" | "LOCKED";

export type UserRecord = Awaited<ReturnType<typeof getUsersData>>[number] & {
  portId?: string | null;
  role: UserRole | string;
  status: UserStatus;
};

export type CreateUserInput = {
  email: string;
  fullName: string;
  password: string;
  portId?: string | null;
  role: UserRole;
  status: UserStatus;
};

export type UpdateUserInput = Omit<CreateUserInput, "password">;

export type UserFilters = { search?: string; role?: string; status?: string; portCode?: string };

export async function getUsers(filters: UserFilters = {}) {
  const params = new URLSearchParams();
  for (const [key, value] of Object.entries(filters)) if (value && value !== "ALL") params.set(key, value);
  const suffix = params.toString() ? `?${params.toString()}` : "";
  return withMockFallback(
    () => requestJson<UserRecord[]>(`/api/users${suffix}`),
    () => getUsersData() as UserRecord[]
  );
}

export async function createUser(input: CreateUserInput) {
  return withMockFallback(
    () => requestJson<UserRecord>("/api/users", {
      body: JSON.stringify(input),
      method: "POST"
    }),
    () => createDemoUser(input) as UserRecord
  );
}

export async function updateUser(userId: string, input: UpdateUserInput) {
  return withMockFallback(
    () => requestJson<UserRecord>(`/api/users/${userId}`, {
      body: JSON.stringify(input),
      method: "PUT"
    }),
    () => updateDemoUser(userId, input) as UserRecord
  );
}

export async function deleteUser(userId: string) {
  return withMockFallback(
    () => requestVoid(`/api/users/${userId}`, { method: "DELETE" }),
    () => deleteDemoUser(userId)
  );
}
