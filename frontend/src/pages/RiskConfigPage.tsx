import { useEffect, useMemo, useState } from "react";
import { Badge } from "../components/common/Badge";
import type { DemoUser } from "../App";
import {
  confirmRiskThresholdImport,
  deleteZoneThresholdOverride,
  getRiskConfig,
  getRiskThresholdTemplate,
  previewRiskThresholdImport,
  saveRiskThresholds,
  saveZoneThresholdOverrides,
  type RiskConfigResponse,
  type RiskLevel,
  type RiskThreshold,
  type RiskThresholdImportPreview,
  type ThresholdOperator,
  type WeatherFactor
} from "../services/riskConfigService";
import type { ChangeEvent, CSSProperties, FormEvent } from "react";

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

function formatThresholdComparison(item: Pick<RiskThreshold, "comparisonOperator" | "thresholdValue"> | undefined) {
  if (!item) return "-";

  // Bảng đã có cột đơn vị riêng, chỉ hiển thị phép so sánh để tránh lặp và xuống dòng.
  return `${item.comparisonOperator} ${Number(item.thresholdValue).toLocaleString("vi-VN")}`;
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

type RiskConfigPageProps = {
  currentUser: DemoUser;
};

export function RiskConfigPage({ currentUser }: RiskConfigPageProps) {
  const isAdmin = currentUser.role === "ADMIN";
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
  const [showImportPanel, setShowImportPanel] = useState(false);
  const [importFile, setImportFile] = useState<File | null>(null);
  const [importPreview, setImportPreview] = useState<RiskThresholdImportPreview | null>(null);
  const [previewingImport, setPreviewingImport] = useState(false);
  const [importReason, setImportReason] = useState("");
  const [confirmingImport, setConfirmingImport] = useState(false);
  const [success, setSuccess] = useState<string | null>(null);

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
    setIsDownloading(true);
    setError(null);

    try {
      const file = await getRiskThresholdTemplate();
      const downloadUrl = URL.createObjectURL(file);
      const link = document.createElement("a");

      // Tạo liên kết tạm để trình duyệt tải file Excel xuống.
      link.href = downloadUrl;
      link.download = "PORMS_RiskThresholds_Template.xlsx";
      document.body.appendChild(link);
      link.click();
      link.remove();

      URL.revokeObjectURL(downloadUrl);
    } catch (caught) {
      setError(
        caught instanceof Error
          ? caught.message
          : "Không thể tải template ngưỡng rủi ro."
      );
    } finally {
      setIsDownloading(false);
    }
  }

  function handleImportFileChange(event: ChangeEvent<HTMLInputElement>) {
    const selectedFile = event.target.files?.[0] ?? null;

    setImportPreview(null);
    setError(null);
    setSuccess(null);

    if (!selectedFile) {
      setImportFile(null);
      return;
    }

    // Đồng bộ định dạng và giới hạn dung lượng với Excel parser ở backend.
    if (!selectedFile.name.toLowerCase().endsWith(".xlsx")) {
      setImportFile(null);
      event.target.value = "";
      setError("Chỉ chấp nhận file Excel có định dạng .xlsx.");
      return;
    }

    if (selectedFile.size > 1024 * 1024) {
      setImportFile(null);
      event.target.value = "";
      setError("File Excel không được lớn hơn 1 MB.");
      return;
    }

    setImportFile(selectedFile);
  }

  async function handlePreviewImport() {
    if (!importFile) {
      setError("Vui lòng chọn file Excel trước khi kiểm tra.");
      return;
    }

    setPreviewingImport(true);
    setImportPreview(null);
    setError(null);
    setSuccess(null);

    try {
      const previewResult = await previewRiskThresholdImport(importFile);
      setImportPreview(previewResult);
    } catch (caught) {
      setError(
        caught instanceof Error
          ? caught.message
          : "Không thể kiểm tra file ngưỡng rủi ro."
      );
    } finally {
      setPreviewingImport(false);
    }
  }

  async function handleConfirmImport() {
    const reason = importReason.trim();

    if (!importFile || !importPreview?.canImport) {
      setError("File phải được kiểm tra và hợp lệ trước khi nhập.");
      return;
    }

    if (reason.length < 5 || reason.length > 500) {
      setError("Lý do thay đổi phải có từ 5 đến 500 ký tự.");
      return;
    }

    setConfirmingImport(true);
    setError(null);
    setSuccess(null);

    try {
      // Backend kiểm tra lại file và database trong cùng transaction.
      const result = await confirmRiskThresholdImport(importFile, reason);

      if (!result.succeeded) {
        setImportPreview(result.preview);
        setError("File không còn hợp lệ. Vui lòng kiểm tra lại các lỗi.");
        return;
      }

      // Response chứa cấu hình mới nhất nên không cần gọi GET lần nữa.
      setConfig(result.response.configuration);
      setThresholds(cloneThresholds(result.response.configuration));
      setSuccess(
        `Nhập ngưỡng thành công: ${result.response.createdCount} tạo mới, `
        + `${result.response.updatedCount} cập nhật, `
        + `${result.response.unchangedCount} không thay đổi.`
      );

      setShowImportPanel(false);
      setImportFile(null);
      setImportPreview(null);
      setImportReason("");
    } catch (caught) {
      setError(
        caught instanceof Error
          ? caught.message
          : "Không thể nhập ngưỡng rủi ro."
      );
    } finally {
      setConfirmingImport(false);
    }
  }

  function closeImportPanel() {
    setShowImportPanel(false);
    setImportFile(null);
    setImportPreview(null);
    setImportReason("");
    setError(null);
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
          <h2>{isAdmin ? "Cấu hình ngưỡng rủi ro" : "Ngưỡng rủi ro đang áp dụng"}</h2>
          <p>{isAdmin ? "Thiết lập ngưỡng thời tiết để phân loại rủi ro vận hành cảng" : "Theo dõi cấu hình an toàn hiện hành của hệ thống ở chế độ chỉ xem"}</p>
        </div>
        <div className="head-actions">
          {isAdmin ? (
            <>
              <button
                className="button button-secondary"
                onClick={() => setThresholds(cloneThresholds(config))}
                type="button"
              >
                Khôi phục
              </button>
              <button
                className="button button-secondary"
                disabled={isDownloading}
                onClick={handleDownloadTemplate}
                type="button"
              >
                {isDownloading ? "Đang tải..." : "Tải template Excel"}
              </button>

              <button
                className="button button-primary"
                onClick={() => {
                  setShowImportPanel(true);
                  setImportFile(null);
                  setImportPreview(null);
                  setImportReason("");
                  setError(null);
                  setSuccess(null);
                }}
                type="button"
              >
                Nhập Excel
              </button>
            </>
          ) : null}

          {isAdmin ? (
            <button
              className="button button-primary"
              disabled={saving}
              onClick={handleSaveThresholds}
              type="button"
            >
              Lưu cấu hình
            </button>
          ) : <Badge tone="info">Chỉ xem</Badge>}
        </div>
      </div>

      {error ? <div className="form-error" role="alert">{error}</div> : null}

      {success ? (
        <div className="form-success" role="status">
          {success}
        </div>
      ) : null}

      {isAdmin && showImportPanel ? (
        <article className="card import-workspace">
          <div className="import-workspace-head">
            <div>
              <span className="page-eyebrow">NHẬP CẤU HÌNH HÀNG LOẠT</span>
              <h3>Nhập ngưỡng rủi ro từ Excel</h3>
              <p>File chỉ được ghi sau khi vượt qua bước kiểm tra và được Admin xác nhận.</p>
            </div>
            <button
              className="button button-secondary button-small"
              onClick={closeImportPanel}
              type="button"
            >
              Đóng
            </button>
          </div>

          <div className="import-step-list" aria-label="Quy trình nhập Excel">
            <span className="is-active"><strong>1</strong> Chọn file</span>
            <span className={importPreview ? "is-complete" : ""}><strong>2</strong> Kiểm tra</span>
            <span className={importPreview?.canImport ? "is-active" : ""}><strong>3</strong> Xác nhận</span>
          </div>

          <label className={`import-dropzone${importFile ? " has-file" : ""}`}>
            <input
              accept=".xlsx,application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
              aria-label="File Excel ngưỡng rủi ro"
              className="import-file-input"
              onChange={handleImportFileChange}
              type="file"
            />
            <span className="import-file-icon" aria-hidden="true">XLSX</span>
            <span className="import-file-copy">
              <strong>{importFile ? importFile.name : "Chọn file ngưỡng rủi ro"}</strong>
              <small>{importFile ? `${Math.max(1, Math.ceil(importFile.size / 1024))} KB · Sẵn sàng kiểm tra` : "Định dạng .xlsx · Dung lượng tối đa 1 MB"}</small>
            </span>
            <span className="button button-secondary">Duyệt file</span>
          </label>

          <div className="import-actions">
            <p><strong>Kiểm tra trước khi ghi:</strong> cấu trúc cột, đơn vị, toán tử và tính liên tục giữa các mức.</p>
            <button
              className="button button-primary"
              disabled={!importFile || previewingImport || confirmingImport}
              onClick={handlePreviewImport}
              type="button"
            >
              {previewingImport ? "Đang kiểm tra..." : "Kiểm tra file"}
            </button>
          </div>

          {importPreview ? (
            <>
              <div className={`risk-preview-result ${importPreview.canImport ? "tone-success" : "tone-warning"}`}>
                <strong>
                  {importPreview.canImport
                    ? "File hợp lệ và có thể nhập."
                    : "File còn lỗi và chưa thể nhập."}
                </strong>
              </div>

              <div className="stats-grid">
                {[
                  ["Tổng dòng", importPreview.totalRows],
                  ["Tạo mới", importPreview.createCount],
                  ["Cập nhật", importPreview.updateCount],
                  ["Không đổi", importPreview.unchangedCount],
                  ["Không hợp lệ", importPreview.invalidRows]
                ].map(([label, value]) => (
                  <article className="card card-pad stat-card" key={label}>
                    <span>{label}</span>
                    <strong>{value}</strong>
                  </article>
                ))}
              </div>

              {importPreview.errors.length > 0 ? (
                <div className="form-error" role="alert">
                  {importPreview.errors.map((item, index) => (
                    <div key={`${item.rowNumber}-${item.column}-${index}`}>
                      Dòng {item.rowNumber}, cột {item.column}: {item.message}
                    </div>
                  ))}
                </div>
              ) : null}

              <div className="table-card">
                <table className="data-table">
                  <thead>
                    <tr>
                      <th>Dòng</th>
                      <th>Yếu tố</th>
                      <th>Mức rủi ro</th>
                      <th>Ngưỡng</th>
                      <th>Hành động</th>
                      <th>Kết quả</th>
                    </tr>
                  </thead>
                  <tbody>
                    {importPreview.rows.map((row) => (
                      <tr key={row.rowNumber}>
                        <td>{row.rowNumber}</td>
                        <td>{row.factor ?? "-"}</td>
                        <td>{row.riskLevel ?? "-"}</td>
                        <td>
                          {row.comparisonOperator ?? "-"} {row.thresholdValue ?? "-"} {row.unit ?? ""}
                        </td>
                        <td>{row.action}</td>
                        <td>{row.errors.map((item) => item.message).join("; ") || "Hợp lệ"}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>

              {importPreview.canImport ? (
                <>
                  <div className="config-form-grid">
                    <label className="wide-field">
                      <span>Lý do thay đổi</span>
                      <textarea
                        aria-label="Lý do thay đổi khi nhập ngưỡng"
                        maxLength={500}
                        onChange={(event) => setImportReason(event.target.value)}
                        placeholder="Ví dụ: Cập nhật ngưỡng thời tiết theo bộ tài liệu đã phê duyệt"
                        rows={3}
                        value={importReason}
                      />
                      <small>
                        {importReason.trim().length}/500 ký tự, tối thiểu 5 ký tự
                      </small>
                    </label>
                  </div>

                  <div className="form-actions">
                    <button
                      className="button button-primary"
                      disabled={
                        confirmingImport
                        || importReason.trim().length < 5
                        || importReason.trim().length > 500
                      }
                      onClick={handleConfirmImport}
                      type="button"
                    >
                      {confirmingImport ? "Đang nhập..." : "Xác nhận nhập dữ liệu"}
                    </button>
                  </div>
                </>
              ) : null}
            </>
          ) : null}
        </article>
      ) : null}

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
                                {isAdmin ? (
                                  <>
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
                                  </>
                                ) : <span className="threshold-readonly-value">{formatThresholdComparison(item)}</span>}
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
        {isAdmin ? <form className="inline-config-form" onSubmit={handleSaveOverride}>
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
        </form> : <div className="readonly-notice"><strong>Chế độ chỉ xem</strong><span>Chỉ Admin được thêm, sửa hoặc xóa ngưỡng riêng của khu vực.</span></div>}
        <div className="override-grid">
          {config?.zoneOverrides.map((zone) => (
            <div className="override-card" key={zone.id}>
              <div>
                <strong>{zone.zoneName}</strong>
                <small>{zone.zoneType} · {zone.riskLevel}: {formatThreshold(zone)}</small>
              </div>
              <div className="row-actions">
                <Badge tone={zone.isEnabled ? "success" : "muted"}>{zone.isEnabled ? "Đang bật" : "Tắt"}</Badge>
                {isAdmin ? <button className="button button-secondary button-small" onClick={() => handleDeleteOverride(zone.zoneId, zone.id)} type="button">Xóa</button> : null}
              </div>
            </div>
          ))}
        </div>
      </article>
    </section>
  );
}
