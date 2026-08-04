import { FormEvent, useEffect, useMemo, useState } from "react";
import { Badge } from "../components/common/Badge";
import {
  deleteZoneThresholdOverride,
  getRiskConfig,
  getRiskThresholdTemplate,
  saveRiskThresholds,
  saveZoneThresholdOverrides,
  type RiskConfigResponse,
  type RiskLevel,
  type RiskThreshold,
  type ThresholdOperator,
  type WeatherFactor
} from "../services/riskConfigService";
import type { CSSProperties } from "react";

const riskLevels = [
  { color: "var(--green)", label: "LOW", note: "Vận hành bình thường", range: "0 - 24" },
  { color: "var(--amber)", label: "MEDIUM", note: "Tăng giám sát", range: "25 - 49" },
  { color: "var(--orange)", label: "HIGH", note: "Hạn chế khu vực", range: "50 - 74" },
  { color: "var(--red)", label: "CRITICAL", note: "Dừng hoạt động", range: "75 - 100" }
] as const;

const factorLabels: Record<WeatherFactor, string> = {
  RAIN: "Mưa 1 giờ",
  VISIBILITY: "Tầm nhìn",
  WAVE: "Sóng",
  WIND: "Gió Beaufort"
};

const factorOrder: WeatherFactor[] = ["WIND", "RAIN", "VISIBILITY"];
const thresholdLevels: RiskLevel[] = ["MEDIUM", "HIGH", "CRITICAL"];
const operators: ThresholdOperator[] = [">=", "<="];

type OverrideForm = {
  comparisonOperator: ThresholdOperator;
  factor: WeatherFactor;
  isEnabled: boolean;
  riskLevel: RiskLevel;
  thresholdValue: string;
  unit: string;
  zoneId: string;
};

const emptyOverride: OverrideForm = {
  comparisonOperator: ">=",
  factor: "WIND",
  isEnabled: true,
  riskLevel: "HIGH",
  thresholdValue: "7",
  unit: "cấp",
  zoneId: ""
};

function thresholdKey(item: Pick<RiskThreshold, "factor" | "riskLevel">) {
  return `${item.factor}:${item.riskLevel}`;
}

function cloneThresholds(config: RiskConfigResponse | null) {
  return config?.thresholds.map((item) => ({ ...item })) ?? [];
}

function formatThreshold(item: Pick<RiskThreshold, "comparisonOperator" | "thresholdValue" | "unit"> | undefined) {
  if (!item) return "-";
  return `${item.comparisonOperator} ${Number(item.thresholdValue).toLocaleString("vi-VN")} ${item.unit}`;
}

function previewRisk(beaufort: number, rain: number, visibility: number, thresholds: RiskThreshold[]) {
  const enabled = thresholds.filter((item) => item.isEnabled);
  const values: Record<string, number> = { RAIN: rain, VISIBILITY: visibility, WIND: beaufort };
  const score: Record<RiskLevel, number> = { CRITICAL: 4, HIGH: 3, LOW: 1, MEDIUM: 2 };
  let result: RiskLevel = "LOW";

  for (const threshold of enabled) {
    const value = values[threshold.factor];
    if (value === undefined) continue;
    const matched = threshold.comparisonOperator === "<="
      ? value <= threshold.thresholdValue
      : value >= threshold.thresholdValue;
    if (matched && score[threshold.riskLevel] > score[result]) {
      result = threshold.riskLevel;
    }
  }

  return result;
}

export function RiskConfigPage() {
  const [config, setConfig] = useState<RiskConfigResponse | null>(null);
  const [thresholds, setThresholds] = useState<RiskThreshold[]>([]);
  const [overrideForm, setOverrideForm] = useState<OverrideForm>(emptyOverride);
  const [beaufort, setBeaufort] = useState(8);
  const [rain, setRain] = useState(28);
  const [visibility, setVisibility] = useState(4.2);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [isDownloading, setIsDownloading] = useState(false);

  useEffect(() => {
    let active = true;
    setLoading(true);
    getRiskConfig()
      .then((data) => {
        if (!active) return;
        setConfig(data);
        setThresholds(cloneThresholds(data));
        setOverrideForm((value) => ({ ...value, zoneId: data.zones[0]?.zoneId ?? "" }));
      })
      .catch((caught) => {
        if (active) setError(caught instanceof Error ? caught.message : "Không thể tải cấu hình ngưỡng.");
      })
      .finally(() => {
        if (active) setLoading(false);
      });
    return () => {
      active = false;
    };
  }, []);

  const thresholdMap = useMemo(() => new Map(thresholds.map((item) => [thresholdKey(item), item])), [thresholds]);
  const preview = previewRisk(beaufort, rain, visibility, thresholds);

  function updateThreshold(factor: WeatherFactor, riskLevel: RiskLevel, patch: Partial<RiskThreshold>) {
    setThresholds((current) => current.map((item) => (
      item.factor === factor && item.riskLevel === riskLevel ? { ...item, ...patch } : item
    )));
  }

  async function handleSaveThresholds() {
    setSaving(true);
    setError(null);
    try {
      const updated = await saveRiskThresholds({
        changeReason: "Cập nhật từ màn hình ngưỡng rủi ro",
        thresholds
      });
      setConfig(updated);
      setThresholds(cloneThresholds(updated));
    } catch (caught) {
      setError(caught instanceof Error ? caught.message : "Không thể lưu cấu hình ngưỡng.");
    } finally {
      setSaving(false);
    }
  }

  async function handleDownloadTemplate() {
  try {
    setIsDownloading(true);

    const file = await getRiskThresholdTemplate();
    const downloadUrl = URL.createObjectURL(file);

    const link = document.createElement("a");
    link.href = downloadUrl;
    link.download = "PORMS_RiskThresholds_Template.xlsx";

    document.body.appendChild(link);
    link.click();
    link.remove();

    URL.revokeObjectURL(downloadUrl);
  } catch (error) {
    const message =
      error instanceof Error
        ? error.message
        : "Không thể tải template.";

    setError(message);
    } finally {
    setIsDownloading(false);
    }
  }

  async function handleSaveOverride(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!overrideForm.zoneId) return;

    setSaving(true);
    setError(null);
    try {
      const updated = await saveZoneThresholdOverrides(overrideForm.zoneId, {
        changeReason: "Cập nhật override khu vực",
        overrides: [{
          comparisonOperator: overrideForm.comparisonOperator,
          factor: overrideForm.factor,
          isEnabled: overrideForm.isEnabled,
          riskLevel: overrideForm.riskLevel,
          thresholdValue: Number(overrideForm.thresholdValue),
          unit: overrideForm.unit
        }]
      });
      setConfig(updated);
      setThresholds(cloneThresholds(updated));
    } catch (caught) {
      setError(caught instanceof Error ? caught.message : "Không thể lưu override khu vực.");
    } finally {
      setSaving(false);
    }
  }

  async function handleDeleteOverride(zoneId: string, overrideId: string) {
    if (!window.confirm("Xóa ngưỡng riêng của khu vực này?")) return;
    setSaving(true);
    setError(null);
    try {
      await deleteZoneThresholdOverride(zoneId, overrideId);
      const updated = await getRiskConfig();
      setConfig(updated);
      setThresholds(cloneThresholds(updated));
    } catch (caught) {
      setError(caught instanceof Error ? caught.message : "Không thể xóa override.");
    } finally {
      setSaving(false);
    }
  }

  if (loading) {
    return <section className="page-grid"><div className="card card-pad">Đang tải cấu hình ngưỡng...</div></section>;
  }

  return (
    <section className="page-grid risk-config-page">
      <div className="section-heading">
        <div>
          <h2>Cấu hình ngưỡng rủi ro</h2>
          <p>Thiết lập ngưỡng thời tiết để phân loại rủi ro vận hành cảng</p>
        </div>
        <div className="head-actions">
  <button
    className="button button-secondary"
    onClick={() => setThresholds(cloneThresholds(config))}
    type="button"
  >
    Khôi phục
  </button>

  <button
    className="button button-secondary"
    type="button"
    disabled={isDownloading}
    onClick={handleDownloadTemplate}
  >
    {isDownloading ? "Đang tải..." : "Tải template Excel"}
  </button>

  <button
    className="button button-primary"
    disabled={saving}
    onClick={handleSaveThresholds}
    type="button"
  >
    Lưu cấu hình
  </button>
</div>
      </div>

      {error ? <div className="form-error" role="alert">{error}</div> : null}

      <div className="stats-grid risk-level-grid">
        {riskLevels.map((level) => (
          <article className="card card-pad risk-level-card" key={level.label} style={{ "--accent": level.color } as CSSProperties}>
            <span>{level.note}</span>
            <strong>{level.label}</strong>
            <small>Điểm rủi ro {level.range}</small>
          </article>
        ))}
      </div>

      <div className="risk-layout-grid">
        <article className="card table-card">
          <div className="card-head card-pad compact-card-head">
            <div>
              <h3>Ngưỡng theo yếu tố thời tiết</h3>
              <p>Áp dụng cho toàn cảng khi khu vực chưa có cấu hình riêng</p>
            </div>
            <Badge tone="info">Database</Badge>
          </div>
          <div className="table-wrap">
            <table className="data-table">
              <thead>
                <tr>
                  <th>Yếu tố</th>
                  <th>Đơn vị</th>
                  {thresholdLevels.map((level) => <th key={level}>{level}</th>)}
                </tr>
              </thead>
              <tbody>
                {factorOrder.map((factor) => {
                  const first = thresholdLevels.map((level) => thresholdMap.get(`${factor}:${level}`)).find(Boolean);
                  return (
                    <tr key={factor}>
                      <td><strong>{factorLabels[factor]}</strong></td>
                      <td>{first?.unit ?? "-"}</td>
                      {thresholdLevels.map((level) => {
                        const item = thresholdMap.get(`${factor}:${level}`);
                        return (
                          <td key={level}>
                            {item ? (
                              <div className="threshold-cell">
                                <select
                                  aria-label={`${factor} ${level} operator`}
                                  onChange={(event) => updateThreshold(factor, level, { comparisonOperator: event.target.value as ThresholdOperator })}
                                  value={item.comparisonOperator}
                                >
                                  {operators.map((operator) => <option key={operator} value={operator}>{operator}</option>)}
                                </select>
                                <input
                                  aria-label={`${factor} ${level} value`}
                                  onChange={(event) => updateThreshold(factor, level, { thresholdValue: Number(event.target.value) })}
                                  type="number"
                                  value={item.thresholdValue}
                                />
                              </div>
                            ) : "-"}
                          </td>
                        );
                      })}
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        </article>

        <article className="card card-pad risk-preview-card">
          <div className="card-head">
            <div>
              <h3>Xem trước đánh giá rủi ro</h3>
              <p>Nhập kịch bản thời tiết để kiểm tra mức rủi ro dự kiến</p>
            </div>
            <Badge tone={preview === "CRITICAL" ? "danger" : preview === "HIGH" ? "warning" : "info"}>{preview}</Badge>
          </div>
          <div className="risk-preview-form">
            <label><span>Gió Beaufort</span><input onChange={(event) => setBeaufort(Number(event.target.value))} type="number" value={beaufort} /></label>
            <label><span>Mưa 1 giờ</span><input onChange={(event) => setRain(Number(event.target.value))} type="number" value={rain} /></label>
            <label><span>Tầm nhìn</span><input onChange={(event) => setVisibility(Number(event.target.value))} type="number" value={visibility} /></label>
          </div>
          <div className="risk-preview-result">
            <span>Kết quả dự kiến</span>
            <strong>{preview}</strong>
            <p>Kết quả được tính theo ngưỡng đang hiển thị trên màn hình trước khi lưu vào database.</p>
          </div>
        </article>
      </div>

      <article className="card card-pad">
        <div className="zone-edit-head">
          <div>
            <h3>Ngưỡng riêng theo khu vực</h3>
            <p>Ưu tiên áp dụng khi khu vực có đặc thù vận hành riêng</p>
          </div>
        </div>
        <form className="inline-config-form" onSubmit={handleSaveOverride}>
          <select aria-label="Khu vực" onChange={(event) => setOverrideForm((value) => ({ ...value, zoneId: event.target.value }))} required value={overrideForm.zoneId}>
            {config?.zones.map((zone) => <option key={zone.zoneId} value={zone.zoneId}>{zone.portName} - {zone.zoneName}</option>)}
          </select>
          <select aria-label="Yếu tố override" onChange={(event) => setOverrideForm((value) => ({ ...value, factor: event.target.value as WeatherFactor }))} value={overrideForm.factor}>
            {factorOrder.map((factor) => <option key={factor} value={factor}>{factorLabels[factor]}</option>)}
          </select>
          <select aria-label="Mức rủi ro override" onChange={(event) => setOverrideForm((value) => ({ ...value, riskLevel: event.target.value as RiskLevel }))} value={overrideForm.riskLevel}>
            {thresholdLevels.map((level) => <option key={level} value={level}>{level}</option>)}
          </select>
          <select aria-label="Toán tử override" onChange={(event) => setOverrideForm((value) => ({ ...value, comparisonOperator: event.target.value as ThresholdOperator }))} value={overrideForm.comparisonOperator}>
            {operators.map((operator) => <option key={operator} value={operator}>{operator}</option>)}
          </select>
          <input aria-label="Giá trị override" onChange={(event) => setOverrideForm((value) => ({ ...value, thresholdValue: event.target.value }))} required type="number" value={overrideForm.thresholdValue} />
          <input aria-label="Đơn vị override" onChange={(event) => setOverrideForm((value) => ({ ...value, unit: event.target.value }))} required value={overrideForm.unit} />
          <button className="button button-secondary" disabled={saving || !overrideForm.zoneId} type="submit">Lưu override</button>
        </form>
        <div className="override-grid">
          {config?.zoneOverrides.map((zone) => (
            <div className="override-card" key={zone.id}>
              <div>
                <strong>{zone.zoneName}</strong>
                <small>{zone.zoneType} · {zone.riskLevel}: {formatThreshold(zone)}</small>
              </div>
              <div className="row-actions">
                <Badge tone={zone.isEnabled ? "success" : "muted"}>{zone.isEnabled ? "Đang bật" : "Tắt"}</Badge>
                <button className="button button-secondary button-small" onClick={() => handleDeleteOverride(zone.zoneId, zone.id)} type="button">Xóa</button>
              </div>
            </div>
          ))}
        </div>
      </article>
    </section>
  );
}
