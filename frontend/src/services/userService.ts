import { getUsers as getUsersData } from "../mock/demoData";
import { requestJson, withMockFallback } from "./api";

export type UserRecord = Awaited<ReturnType<typeof getUsersData>>[number];

export async function getUsers() {
  return withMockFallback(
    () => requestJson<UserRecord[]>("/api/users"),
    () => getUsersData()
  );
}
