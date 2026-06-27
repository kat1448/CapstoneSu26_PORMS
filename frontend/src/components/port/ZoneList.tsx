import { FormEvent, useState } from "react";
import type { UpdateZoneInput } from "../../services/portService";
import type { PortZone } from "../../types/port";
import { Badge } from "../common/Badge";

type ZoneListProps = {
  onDeleteZone: (zone: PortZone) => Promise<void>;
  onUpdateZone: (zone: PortZone, input: UpdateZoneInput) => Promise<void>;
  zones: PortZone[];
};

type ZoneFormState = {
  capacityUnit: string;
  capacityValue: string;
  displayOrder: string;
  isActive: boolean;
  latitude: string;
  longitude: string;
  name: string;
  zoneType: PortZone["zoneType"];
};

function riskTone(risk: PortZone["currentRiskLevel"]) {
  if (risk === "CRITICAL") return "danger";
  if (risk === "HIGH") return "warning";
  if (risk === "MEDIUM") return "info";
  return "success";
}

function numberOrNull(value: string): number | null {
  return value.trim() ? Number(value) : null;
}

function parseCapacityValue(label: string) {
  const match = label.match(/^(\d+(?:\.\d+)?)/);
  return match ? match[1] : "";
}

function parseCapacityUnit(label: string) {
  const match = label.match(/^\d+(?:\.\d+)?\s+(.+)$/);
  return match ? match[1] : "";
}

function toFormState(zone: PortZone): ZoneFormState {
  return {
    capacityUnit: zone.capacityUnit ?? parseCapacityUnit(zone.capacityLabel),
    capacityValue: zone.capacityValue == null ? parseCapacityValue(zone.capacityLabel) : String(zone.capacityValue),
    displayOrder: String(zone.displayOrder),
    isActive: zone.isActive,
    latitude: zone.latitude == null ? "" : String(zone.latitude),
    longitude: zone.longitude == null ? "" : String(zone.longitude),
    name: zone.zoneName,
    zoneType: zone.zoneType
  };
}

export function ZoneList({ onDeleteZone, onUpdateZone, zones }: ZoneListProps) {
  const [editingZone, setEditingZone] = useState<PortZone | null>(null);
  const [form, setForm] = useState<ZoneFormState | null>(null);
  const [submitting, setSubmitting] = useState(false);

  function startEdit(zone: PortZone) {
    setEditingZone(zone);
    setForm(toFormState(zone));
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!editingZone || !form) return;

    setSubmitting(true);
    try {
      await onUpdateZone(editingZone, {
        capacityUnit: form.capacityUnit.trim() || null,
        capacityValue: numberOrNull(form.capacityValue),
        displayOrder: Number(form.displayOrder),
        isActive: form.isActive,
        latitude: numberOrNull(form.latitude),
        longitude: numberOrNull(form.longitude),
        name: form.name.trim(),
        zoneType: form.zoneType
      });
      setEditingZone(null);
      setForm(null);
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <>
      <div className="card table-card">
        <table className="data-table">
          <thead>
            <tr>
              <th>Khu vực</th>
              <th>Loại</th>
              <th>Sức chứa</th>
              <th>Rủi ro</th>
              <th>Trạng thái</th>
              <th>Thao tác</th>
            </tr>
          </thead>
          <tbody>
            {zones.map((zone) => (
              <tr key={zone.zoneId}>
                <td>{zone.zoneName}</td>
                <td>{zone.zoneType}</td>
                <td>{zone.capacityLabel}</td>
                <td>
                  <Badge tone={riskTone(zone.currentRiskLevel)}>{zone.currentRiskLevel}</Badge>
                </td>
                <td>{zone.statusLabel}</td>
                <td>
                  <div className="table-actions">
                    <button
                      aria-label={`Chỉnh sửa ${zone.zoneName}`}
                      className="button button-secondary button-small"
                      onClick={() => startEdit(zone)}
                      type="button"
                    >
                      Chỉnh sửa
                    </button>
                    <button
                      aria-label={`Xóa ${zone.zoneName}`}
                      className="button button-danger button-small"
                      onClick={() => void onDeleteZone(zone)}
                      type="button"
                    >
                      Xóa
                    </button>
                  </div>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {editingZone && form ? (
        <form className="card card-pad port-create-form" onSubmit={handleSubmit}>
          <div className="card-head">
            <div>
              <h3>Chỉnh sửa khu vực</h3>
              <p>{editingZone.zoneName}</p>
            </div>
            <button
              className="button button-secondary"
              onClick={() => {
                setEditingZone(null);
                setForm(null);
              }}
              type="button"
            >
              Hủy
            </button>
          </div>

          <div className="port-form-grid">
            <label>
              <span>Tên khu vực</span>
              <input
                onChange={(event) => setForm((value) => value ? { ...value, name: event.target.value } : value)}
                required
                value={form.name}
              />
            </label>
            <label>
              <span>Loại khu vực</span>
              <select
                onChange={(event) => setForm((value) => value ? { ...value, zoneType: event.target.value as PortZone["zoneType"] } : value)}
                value={form.zoneType}
              >
                <option value="DOCK">DOCK</option>
                <option value="YARD">YARD</option>
                <option value="GATE">GATE</option>
                <option value="WAREHOUSE">WAREHOUSE</option>
              </select>
            </label>
            <label>
              <span>Sức chứa</span>
              <input
                onChange={(event) => setForm((value) => value ? { ...value, capacityValue: event.target.value } : value)}
                type="number"
                value={form.capacityValue}
              />
            </label>
            <label>
              <span>Đơn vị</span>
              <input
                onChange={(event) => setForm((value) => value ? { ...value, capacityUnit: event.target.value } : value)}
                value={form.capacityUnit}
              />
            </label>
            <label>
              <span>Latitude</span>
              <input
                onChange={(event) => setForm((value) => value ? { ...value, latitude: event.target.value } : value)}
                step="0.000001"
                type="number"
                value={form.latitude}
              />
            </label>
            <label>
              <span>Longitude</span>
              <input
                onChange={(event) => setForm((value) => value ? { ...value, longitude: event.target.value } : value)}
                step="0.000001"
                type="number"
                value={form.longitude}
              />
            </label>
            <label>
              <span>Thứ tự</span>
              <input
                onChange={(event) => setForm((value) => value ? { ...value, displayOrder: event.target.value } : value)}
                type="number"
                value={form.displayOrder}
              />
            </label>
            <label className="checkbox-field">
              <input
                checked={form.isActive}
                onChange={(event) => setForm((value) => value ? { ...value, isActive: event.target.checked } : value)}
                type="checkbox"
              />
              <span>Đang hoạt động</span>
            </label>
          </div>

          <div className="form-actions">
            <button className="button button-primary" disabled={submitting} type="submit">
              Lưu khu vực
            </button>
          </div>
        </form>
      ) : null}
    </>
  );
}
