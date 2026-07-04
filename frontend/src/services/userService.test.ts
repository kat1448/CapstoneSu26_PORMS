import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { createUser, deleteUser, updateUser } from "./userService";

describe("userService", () => {
  beforeEach(() => {
    localStorage.clear();
    vi.stubGlobal("fetch", vi.fn());
  });

  afterEach(() => {
    vi.unstubAllGlobals();
    localStorage.clear();
  });

  it("creates a user through the API", async () => {
    const response = {
      email: "mai@example.com",
      fullName: "Le Thi Mai",
      lastLoginLabel: "Chưa đăng nhập",
      portId: "port-dntsa",
      portName: "Cảng Tiên Sa",
      role: "ADMIN",
      status: "ACTIVE",
      userId: "user-10"
    };
    vi.mocked(fetch).mockResolvedValueOnce(new Response(JSON.stringify(response), { status: 201 }));

    await expect(createUser({
      email: "mai@example.com",
      fullName: "Le Thi Mai",
      password: "Strong@2027!",
      portId: "port-dntsa",
      role: "ADMIN",
      status: "ACTIVE"
    })).resolves.toEqual(response);

    expect(fetch).toHaveBeenCalledWith("http://localhost:5000/api/users", expect.objectContaining({
      body: JSON.stringify({
        email: "mai@example.com",
        fullName: "Le Thi Mai",
        password: "Strong@2027!",
        portId: "port-dntsa",
        role: "ADMIN",
        status: "ACTIVE"
      }),
      method: "POST"
    }));
  });

  it("updates a user through the API", async () => {
    const response = {
      email: "mai.updated@example.com",
      fullName: "Le Thi Mai Updated",
      lastLoginLabel: "Chưa đăng nhập",
      portId: "port-dntsa",
      portName: "Cảng Tiên Sa",
      role: "STANDARD_USER",
      status: "ACTIVE",
      userId: "user-10"
    };
    vi.mocked(fetch).mockResolvedValueOnce(new Response(JSON.stringify(response), { status: 200 }));

    await expect(updateUser("user-10", {
      email: "mai.updated@example.com",
      fullName: "Le Thi Mai Updated",
      portId: "port-dntsa",
      role: "STANDARD_USER",
      status: "ACTIVE"
    })).resolves.toEqual(response);

    expect(fetch).toHaveBeenCalledWith("http://localhost:5000/api/users/user-10", expect.objectContaining({
      method: "PUT"
    }));
  });

  it("deletes a user through the API", async () => {
    vi.mocked(fetch).mockResolvedValueOnce(new Response(null, { status: 204 }));

    await expect(deleteUser("user-10")).resolves.toBeUndefined();

    expect(fetch).toHaveBeenCalledWith("http://localhost:5000/api/users/user-10", expect.objectContaining({
      method: "DELETE"
    }));
  });
});
