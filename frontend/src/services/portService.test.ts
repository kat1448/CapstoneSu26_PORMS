import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { createPort, deletePortZone, updatePortZone } from "./portService";

describe("portService", () => {
  beforeEach(() => {
    localStorage.clear();
    vi.stubGlobal("fetch", vi.fn());
  });

  afterEach(() => {
    vi.unstubAllGlobals();
    localStorage.clear();
  });

  it("creates a port and zones through the real API without mock fallback", async () => {
    const response = {
      activeAlertCount: 0,
      currentOperationMode: "NORMAL",
      currentRiskLevel: "LOW",
      isActive: true,
      portCode: "DNNEW",
      portId: "11111111-1111-1111-1111-111111111111",
      portName: "Cang Moi",
      updatedAtLabel: "Chưa có dữ liệu"
    };
    vi.mocked(fetch).mockResolvedValueOnce(new Response(JSON.stringify(response), { status: 201 }));

    await expect(createPort({
      address: "Da Nang",
      code: "DNNEW",
      isActive: true,
      latitude: 16.12,
      longitude: 108.22,
      name: "Cang Moi",
      timezone: "Asia/Ho_Chi_Minh",
      weatherSource: "OPENWEATHER",
      weatherStationId: "station-1",
      zones: [{
        capacityUnit: "TEU",
        capacityValue: 1000,
        displayOrder: 1,
        latitude: 16.121,
        longitude: 108.221,
        name: "Bai A",
        zoneType: "YARD"
      }]
    })).resolves.toEqual(response);

    expect(fetch).toHaveBeenCalledWith("http://localhost:5000/api/ports", expect.objectContaining({
      body: JSON.stringify({
        address: "Da Nang",
        code: "DNNEW",
        isActive: true,
        latitude: 16.12,
        longitude: 108.22,
        name: "Cang Moi",
        timezone: "Asia/Ho_Chi_Minh",
        weatherSource: "OPENWEATHER",
        weatherStationId: "station-1",
        zones: [{
          capacityUnit: "TEU",
          capacityValue: 1000,
          displayOrder: 1,
          latitude: 16.121,
          longitude: 108.221,
          name: "Bai A",
          zoneType: "YARD"
        }]
      }),
      method: "POST"
    }));
  });

  it("updates a zone through the real API without mock fallback", async () => {
    const response = {
      capacityLabel: "1200 TEU",
      currentRiskLevel: "LOW",
      displayOrder: 2,
      isActive: true,
      isRestricted: false,
      latitude: 16.121,
      longitude: 108.221,
      overrideEnabled: false,
      portId: "11111111-1111-1111-1111-111111111111",
      restrictionReason: null,
      statusLabel: "Binh thuong",
      zoneId: "22222222-2222-2222-2222-222222222222",
      zoneName: "Bai B",
      zoneType: "YARD"
    };
    vi.mocked(fetch).mockResolvedValueOnce(new Response(JSON.stringify(response), { status: 200 }));

    await expect(updatePortZone(
      "11111111-1111-1111-1111-111111111111",
      "22222222-2222-2222-2222-222222222222",
      {
        capacityUnit: "TEU",
        capacityValue: 1200,
        displayOrder: 2,
        isActive: true,
        latitude: 16.121,
        longitude: 108.221,
        name: "Bai B",
        zoneType: "YARD"
      }
    )).resolves.toEqual(response);

    expect(fetch).toHaveBeenCalledWith(
      "http://localhost:5000/api/ports/11111111-1111-1111-1111-111111111111/zones/22222222-2222-2222-2222-222222222222",
      expect.objectContaining({
        body: JSON.stringify({
          capacityUnit: "TEU",
          capacityValue: 1200,
          displayOrder: 2,
          isActive: true,
          latitude: 16.121,
          longitude: 108.221,
          name: "Bai B",
          zoneType: "YARD"
        }),
        method: "PUT"
      })
    );
  });

  it("deletes a zone through the real API without mock fallback", async () => {
    vi.mocked(fetch).mockResolvedValueOnce(new Response(null, { status: 204 }));

    await expect(deletePortZone(
      "11111111-1111-1111-1111-111111111111",
      "22222222-2222-2222-2222-222222222222"
    )).resolves.toBeUndefined();

    expect(fetch).toHaveBeenCalledWith(
      "http://localhost:5000/api/ports/11111111-1111-1111-1111-111111111111/zones/22222222-2222-2222-2222-222222222222",
      expect.objectContaining({ method: "DELETE" })
    );
  });
});
