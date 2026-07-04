import { FormEvent, useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { createPort, type CreatePortInput, type CreateZoneInput } from "../services/portService";
import type { PortZone } from "../types/port";

type PortFormState = {
  address: string;
  code: string;
  isActive: boolean;
  latitude: string;
  longitude: string;
  name: string;
  timezone: string;
  weatherSource: string;
  weatherStationId: string;
};

type ZoneFormRow = {
  capacityUnit: string;
  capacityValue: string;
  displayOrder: string;
  latitude: string;
  longitude: string;
  name: string;
  zoneType: PortZone["zoneType"];
};

const emptyPortForm: PortFormState = {
  address: "",
  code: "",
  isActive: true,
  latitude: "16.1228",
  longitude: "108.2144",
  name: "",
  timezone: "Asia/Ho_Chi_Minh",
  weatherSource: "OPENWEATHER",
  weatherStationId: ""
};

const firstZoneRow: ZoneFormRow = {
  capacityUnit: "",
  capacityValue: "",
  displayOrder: "1",
  latitude: "",
  longitude: "",
  name: "",
  zoneType: "DOCK"
};

function numberOrNull(value: string): number | null {
  return value.trim() ? Number(value) : null;
}

function toZoneInput(row: ZoneFormRow, index: number): CreateZoneInput {
  return {
    capacityUnit: row.capacityUnit.trim() || null,
    capacityValue: numberOrNull(row.capacityValue),
    displayOrder: Number(row.displayOrder || index + 1),
    latitude: numberOrNull(row.latitude),
    longitude: numberOrNull(row.longitude),
    name: row.name.trim(),
    zoneType: row.zoneType
  };
}

export function PortCreatePage() {
  const navigate = useNavigate();
  const [portForm, setPortForm] = useState<PortFormState>(emptyPortForm);
  const [zoneRows, setZoneRows] = useState<ZoneFormRow[]>([firstZoneRow]);
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  function addZoneRow() {
    setZoneRows((rows) => [...rows, { ...firstZoneRow, displayOrder: String(rows.length + 1) }]);
  }

  function removeZoneRow(index: number) {
    setZoneRows((rows) => rows.filter((_, rowIndex) => rowIndex !== index));
  }

  function updateZoneRow(index: number, patch: Partial<ZoneFormRow>) {
    setZoneRows((rows) => rows.map((row, rowIndex) => rowIndex === index ? { ...row, ...patch } : row));
  }

  async function handleCreatePort(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError(null);
    setSubmitting(true);

    try {
      const input: CreatePortInput = {
        address: portForm.address.trim() || null,
        code: portForm.code.trim(),
        isActive: portForm.isActive,
        latitude: Number(portForm.latitude),
        longitude: Number(portForm.longitude),
        name: portForm.name.trim(),
        timezone: portForm.timezone.trim(),
        weatherSource: portForm.weatherSource.trim(),
        weatherStationId: portForm.weatherStationId.trim() || null,
        zones: zoneRows.filter((row) => row.name.trim()).map(toZoneInput)
      };

      await createPort(input);
      navigate("/ports");
    } catch (caught) {
      setError(caught instanceof Error ? caught.message : "Không thể tạo cảng.");
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <section className="page-grid">
      <div className="section-heading">
        <div>
          <h2>Tạo cảng mới</h2>
          <p>Thông tin cảng và các khu vực ban đầu sẽ được ghi vào cơ sở dữ liệu</p>
        </div>
        <Link className="button button-secondary" to="/ports">
          Quay lại
        </Link>
      </div>

      <article className="card card-pad port-create-card">
        <form className="port-create-form" onSubmit={handleCreatePort}>
          <div className="port-form-grid">
            <label>
              <span>Mã cảng</span>
              <input
                onChange={(event) => setPortForm((value) => ({ ...value, code: event.target.value }))}
                required
                value={portForm.code}
              />
            </label>
            <label>
              <span>Tên cảng</span>
              <input
                onChange={(event) => setPortForm((value) => ({ ...value, name: event.target.value }))}
                required
                value={portForm.name}
              />
            </label>
            <label>
              <span>Địa chỉ</span>
              <input
                onChange={(event) => setPortForm((value) => ({ ...value, address: event.target.value }))}
                value={portForm.address}
              />
            </label>
            <label>
              <span>Latitude cảng</span>
              <input
                onChange={(event) => setPortForm((value) => ({ ...value, latitude: event.target.value }))}
                required
                step="0.000001"
                type="number"
                value={portForm.latitude}
              />
            </label>
            <label>
              <span>Longitude cảng</span>
              <input
                onChange={(event) => setPortForm((value) => ({ ...value, longitude: event.target.value }))}
                required
                step="0.000001"
                type="number"
                value={portForm.longitude}
              />
            </label>
            <label>
              <span>Timezone</span>
              <input
                onChange={(event) => setPortForm((value) => ({ ...value, timezone: event.target.value }))}
                required
                value={portForm.timezone}
              />
            </label>
            <label>
              <span>Nguồn thời tiết</span>
              <input
                onChange={(event) => setPortForm((value) => ({ ...value, weatherSource: event.target.value }))}
                required
                value={portForm.weatherSource}
              />
            </label>
            <label>
              <span>Mã trạm thời tiết</span>
              <input
                onChange={(event) => setPortForm((value) => ({ ...value, weatherStationId: event.target.value }))}
                value={portForm.weatherStationId}
              />
            </label>
            <label className="checkbox-field">
              <input
                checked={portForm.isActive}
                onChange={(event) => setPortForm((value) => ({ ...value, isActive: event.target.checked }))}
                type="checkbox"
              />
              <span>Đang hoạt động</span>
            </label>
          </div>

          <div className="zone-edit-head">
            <div>
              <h3>Khu vực trong cảng</h3>
              <p>Thêm các bến, bãi, cổng hoặc kho thuộc cảng mới</p>
            </div>
            <button className="button button-secondary" onClick={addZoneRow} type="button">
              Thêm khu vực
            </button>
          </div>

          <div className="zone-edit-table" aria-label="Bảng thêm khu vực">
            <div className="zone-edit-row zone-edit-row-head">
              <span>Tên khu vực</span>
              <span>Loại</span>
              <span>Sức chứa</span>
              <span>Đơn vị</span>
              <span>Latitude</span>
              <span>Longitude</span>
              <span>Thứ tự</span>
              <span></span>
            </div>
            {zoneRows.map((row, index) => (
              <div className="zone-edit-row" key={index}>
                <input
                  aria-label={`Tên khu vực ${index + 1}`}
                  onChange={(event) => updateZoneRow(index, { name: event.target.value })}
                  value={row.name}
                />
                <select
                  aria-label={`Loại khu vực ${index + 1}`}
                  onChange={(event) => updateZoneRow(index, { zoneType: event.target.value as PortZone["zoneType"] })}
                  value={row.zoneType}
                >
                  <option value="DOCK">DOCK</option>
                  <option value="YARD">YARD</option>
                  <option value="GATE">GATE</option>
                  <option value="WAREHOUSE">WAREHOUSE</option>
                </select>
                <input
                  aria-label={`Sức chứa ${index + 1}`}
                  onChange={(event) => updateZoneRow(index, { capacityValue: event.target.value })}
                  type="number"
                  value={row.capacityValue}
                />
                <input
                  aria-label={`Đơn vị ${index + 1}`}
                  onChange={(event) => updateZoneRow(index, { capacityUnit: event.target.value })}
                  value={row.capacityUnit}
                />
                <input
                  aria-label={`Latitude khu vực ${index + 1}`}
                  onChange={(event) => updateZoneRow(index, { latitude: event.target.value })}
                  step="0.000001"
                  type="number"
                  value={row.latitude}
                />
                <input
                  aria-label={`Longitude khu vực ${index + 1}`}
                  onChange={(event) => updateZoneRow(index, { longitude: event.target.value })}
                  step="0.000001"
                  type="number"
                  value={row.longitude}
                />
                <input
                  aria-label={`Thứ tự ${index + 1}`}
                  onChange={(event) => updateZoneRow(index, { displayOrder: event.target.value })}
                  type="number"
                  value={row.displayOrder}
                />
                <button
                  aria-label={`Xoá khu vực ${index + 1}`}
                  className="button button-ghost button-small"
                  disabled={zoneRows.length === 1}
                  onClick={() => removeZoneRow(index)}
                  type="button"
                >
                  Xoá
                </button>
              </div>
            ))}
          </div>

          {error ? <div className="form-error" role="alert">{error}</div> : null}

          <div className="form-actions">
            <button className="button button-primary" disabled={submitting} type="submit">
              Tạo cảng
            </button>
          </div>
        </form>
      </article>
    </section>
  );
}
