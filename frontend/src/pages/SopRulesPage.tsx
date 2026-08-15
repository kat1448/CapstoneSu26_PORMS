import {
  useEffect,
  useMemo,
  useState,
  type ChangeEvent,
  type FormEvent
} from "react";
import { Badge } from "../components/common/Badge";
import type { DemoUser } from "../App";
import {
  createSopRule,
  deleteSopRule,
  getSopRuleImportTemplate,
  getSopRules,
  previewSopRuleImport,
  updateSopRule,
  confirmSopRuleImport,
  type SopActionType,
  type SopRule,
  type SopRuleImportPreview,
  type SopRuleInput,
  type SopRulesResponse
} from "../services/sopRulesService";
import type { RiskLevel } from "../services/riskConfigService";

const riskOptions: RiskLevel[] = ["LOW", "MEDIUM", "HIGH", "CRITICAL"];
const zoneOptions = ["", "DOCK", "YARD", "GATE", "WAREHOUSE"];
const actionOptions: SopActionType[] = [
  "SEND_ALERT",
  "RESTRICT_ZONE",
  "SET_LIMITED_MODE",
  "STOP_OPERATIONS",
  "CREATE_TASK",
  "UNRESTRICT_ZONE",
  "SET_NORMAL_MODE"
];

const emptyForm: SopRuleInput = {
  actionType: "CREATE_TASK",
  appliesToZoneType: null,
  changeReason: "Cập nhật từ màn hình SOP",
  description: "",
  isActive: true,
  previousRiskLevel: null,
  ruleCode: "",
  ruleName: "",
  triggerRiskLevel: "HIGH"
};

function riskTone(risk: string): "danger" | "info" | "success" | "warning" {
  if (risk === "CRITICAL") return "danger";
  if (risk === "HIGH") return "warning";
  if (risk === "LOW") return "success";
  return "info";
}

function formFromRule(rule: SopRule): SopRuleInput {
  return {
    actionConfigText: rule.actionConfigText,
    actionType: rule.actionType,
    appliesToZoneType: rule.appliesToZoneType,
    changeReason: "Cập nhật từ màn hình SOP",
    description: rule.description,
    executionOrder: rule.executionOrder,
    isActive: rule.isActive,
    previousRiskLevel: rule.previousRiskLevel,
    ruleCode: rule.ruleCode,
    ruleName: rule.ruleName,
    triggerRiskLevel: rule.triggerRiskLevel
  };
}

function matchesRule(rule: SopRule, query: string, zoneFilter: string) {
  const normalized = query.trim().toLowerCase();
  const textMatched = !normalized
    || rule.ruleCode.toLowerCase().includes(normalized)
    || rule.ruleName.toLowerCase().includes(normalized);
  const zoneMatched = !zoneFilter || rule.appliesToZoneType === zoneFilter;
  return textMatched && zoneMatched;
}

type SopRulesPageProps = {
  currentUser: DemoUser;
};

export function SopRulesPage({ currentUser }: SopRulesPageProps) {
  const isAdmin = currentUser.role === "ADMIN";
  const [data, setData] = useState<SopRulesResponse | null>(null);
  const [form, setForm] = useState<SopRuleInput>(emptyForm);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [query, setQuery] = useState("");
  const [selectedRisk, setSelectedRisk] = useState<RiskLevel | null>(null);
  const [zoneFilter, setZoneFilter] = useState("");
  const [showForm, setShowForm] = useState(false);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [downloadingTemplate, setDownloadingTemplate] = useState(false);
  const [showImportPanel, setShowImportPanel] = useState(false);
  const [importFile, setImportFile] = useState<File | null>(null);
  const [importPreview, setImportPreview] = useState<SopRuleImportPreview | null>(null);
  const [previewingImport, setPreviewingImport] = useState(false);
  const [importReason, setImportReason] = useState("");
  const [confirmingImport, setConfirmingImport] = useState(false);
  const [success, setSuccess] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  async function reload() {
    const response = await getSopRules();
    setData(response);
  }

  useEffect(() => {
    let active = true;
    setLoading(true);
    getSopRules()
      .then((response) => {
        if (active) setData(response);
      })
      .catch((caught) => {
        if (active) setError(caught instanceof Error ? caught.message : "Không thể tải quy tắc SOP.");
      })
      .finally(() => {
        if (active) setLoading(false);
      });
    return () => {
      active = false;
    };
  }, []);

  const groupedRules = useMemo(() => {
    const rules = data?.rules ?? [];
    return riskOptions.map((riskLevel) => {
      const groupRules = rules
        .filter((rule) => rule.triggerRiskLevel === riskLevel && matchesRule(rule, query, zoneFilter))
        .sort((left, right) => left.executionOrder - right.executionOrder);
      const actionCounts = groupRules.reduce<Record<string, number>>((counts, rule) => ({
        ...counts,
        [rule.actionType]: (counts[rule.actionType] ?? 0) + 1
      }), {});
      const topAction = Object.entries(actionCounts).sort((a, b) => b[1] - a[1])[0]?.[0] ?? "Chưa có";

      return {
        activeCount: groupRules.filter((rule) => rule.isActive).length,
        riskLevel,
        rules: groupRules,
        topAction
      };
    });
  }, [data?.rules, query, zoneFilter]);

  const selectedGroup = groupedRules.find((group) => group.riskLevel === selectedRisk) ?? null;

  function openCreateForm() {
    setEditingId(null);
    setForm(selectedRisk ? { ...emptyForm, triggerRiskLevel: selectedRisk } : emptyForm);
    setShowForm(true);
  }

  function openEditForm(rule: SopRule) {
    setEditingId(rule.id);
    setForm(formFromRule(rule));
    setShowForm(true);
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setSaving(true);
    setError(null);
    try {
      const payload = {
        ...form,
        appliesToZoneType: form.appliesToZoneType || null,
        description: form.description?.trim() || null,
        ruleCode: form.ruleCode.trim(),
        ruleName: form.ruleName.trim()
      };
      if (editingId) {
        await updateSopRule(editingId, payload);
      } else {
        await createSopRule(payload);
      }
      await reload();
      setShowForm(false);
      setEditingId(null);
      setForm(emptyForm);
    } catch (caught) {
      setError(caught instanceof Error ? caught.message : "Không thể lưu quy tắc SOP.");
    } finally {
      setSaving(false);
    }
  }

  async function handleDelete(rule: SopRule) {
    if (!window.confirm(`Xóa quy tắc ${rule.ruleCode}?`)) return;
    setSaving(true);
    setError(null);
    try {
      await deleteSopRule(rule.id);
      await reload();
    } catch (caught) {
      setError(caught instanceof Error ? caught.message : "Không thể xóa quy tắc SOP.");
    } finally {
      setSaving(false);
    }
  }

  async function handleDownloadTemplate() {
    setDownloadingTemplate(true);
    setError(null);

    try {
      const template = await getSopRuleImportTemplate();
      const downloadUrl = URL.createObjectURL(template);
      const anchor = document.createElement("a");

      // Tạo liên kết tạm để trình duyệt tải file Excel xuống.
      anchor.href = downloadUrl;
      anchor.download = "PORMS_SOP_Rules_Import_Template.xlsx";
      document.body.appendChild(anchor);
      anchor.click();
      anchor.remove();

      URL.revokeObjectURL(downloadUrl);
    } catch (caught) {
      setError(
        caught instanceof Error
          ? caught.message
          : "Không thể tải template SOP."
      );
    } finally {
      setDownloadingTemplate(false);
    }
  }

  function handleImportFileChange(event: ChangeEvent<HTMLInputElement>) {
    const selectedFile = event.target.files?.[0] ?? null;

    setImportPreview(null);
    setError(null);

    if (!selectedFile) {
      setImportFile(null);
      return;
    }

    // Chỉ nhận định dạng template mà backend hỗ trợ.
    if (!selectedFile.name.toLowerCase().endsWith(".xlsx")) {
      setImportFile(null);
      event.target.value = "";
      setError("Chỉ chấp nhận file Excel có định dạng .xlsx.");
      return;
    }

    // Đồng bộ với giới hạn 1 MB của Excel parser ở backend.
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

    try {
      const preview = await previewSopRuleImport(importFile);
      setImportPreview(preview);
    } catch (caught) {
      setError(
        caught instanceof Error
          ? caught.message
          : "Không thể kiểm tra file SOP."
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
      // Backend sẽ kiểm tra lại file và database trong transaction.
      const result = await confirmSopRuleImport(importFile, reason);

      if (!result.succeeded) {
        setImportPreview(result.preview);
        setError("File không còn hợp lệ. Vui lòng kiểm tra lại các lỗi.");
        return;
      }

      // Response đã chứa cấu hình mới nhất nên không cần gọi GET lần nữa.
      setData(result.response.configuration);
      setSuccess(
        `Nhập SOP thành công: ${result.response.createdCount} tạo mới, `
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
          : "Không thể nhập quy tắc SOP."
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

  if (loading) {
    return <section className="page-grid"><div className="card card-pad">Đang tải quy tắc SOP...</div></section>;
  }

  const summary = data?.summary ?? { activeRules: 0, automatedTasks: 0, recentExecutions: 0, totalRules: 0 };
  const kpis = [
    { label: "Tổng quy tắc", value: summary.totalRules, foot: "Quy tắc trong database" },
    { label: "Đang bật", value: summary.activeRules, foot: "Sẵn sàng kích hoạt" },
    { label: "Nhiệm vụ tự động", value: summary.automatedTasks, foot: "Action CREATE_TASK" }
  ];

  return (
    <section className="page-grid sop-page">
      <div className="section-heading">
        <div>
          <h2>{isAdmin ? "Quy tắc SOP" : "Quy trình ứng phó đang áp dụng"}</h2>
          <p>{isAdmin ? "Quản lý quy tắc tự động kích hoạt hành động vận hành theo mức rủi ro" : "Tra cứu hành động vận hành theo mức rủi ro ở chế độ chỉ xem"}</p>
        </div>
        <div className="head-actions">
          <button className="button button-secondary" onClick={reload} type="button">Tải lại</button>

          {isAdmin ? (
            <>
              <button
                className="button button-secondary"
                disabled={downloadingTemplate}
                onClick={handleDownloadTemplate}
                type="button"
              >
                {downloadingTemplate ? "Đang tải..." : "Tải template Excel"}
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

          {isAdmin ? <button className="button button-primary" onClick={openCreateForm} type="button">Thêm quy tắc</button> : <Badge tone="info">Chỉ xem</Badge>}
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
              <span className="page-eyebrow">NHẬP QUY TRÌNH HÀNG LOẠT</span>
              <h3>Nhập quy tắc SOP từ Excel</h3>
              <p>Xem trước toàn bộ thay đổi trước khi cập nhật quy trình vận hành.</p>
            </div>
            <button className="button button-secondary button-small" onClick={closeImportPanel} type="button">
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
              aria-label="File Excel SOP"
              className="import-file-input"
              onChange={handleImportFileChange}
              type="file"
            />
            <span className="import-file-icon" aria-hidden="true">XLSX</span>
            <span className="import-file-copy">
              <strong>{importFile ? importFile.name : "Chọn file quy tắc SOP"}</strong>
              <small>{importFile ? `${Math.max(1, Math.ceil(importFile.size / 1024))} KB · Sẵn sàng kiểm tra` : "Định dạng .xlsx · Dung lượng tối đa 1 MB"}</small>
            </span>
            <span className="button button-secondary">Duyệt file</span>
          </label>

          <div className="import-actions">
            <p><strong>Kiểm tra trước khi ghi:</strong> mã quy tắc, mức rủi ro, loại khu vực và hành động SOP.</p>
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
                <strong>{importPreview.canImport ? "File hợp lệ và có thể nhập." : "File còn lỗi và chưa thể nhập."}</strong>
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
                    <tr><th>Dòng</th><th>Mã</th><th>Hành động</th><th>Kết quả</th></tr>
                  </thead>
                  <tbody>
                    {importPreview.rows.map((row) => (
                      <tr key={row.rowNumber}>
                        <td>{row.rowNumber}</td>
                        <td>{row.ruleCode ?? "-"}</td>
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
                        aria-label="Lý do thay đổi khi nhập SOP"
                        maxLength={500}
                        onChange={(event) => setImportReason(event.target.value)}
                        placeholder="Ví dụ: Cập nhật bộ quy tắc SOP theo tài liệu đã phê duyệt"
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

      <div className="stats-grid">
        {kpis.map((item) => (
          <article className="card card-pad stat-card" key={item.label}>
            <span>{item.label}</span>
            <strong>{item.value}</strong>
            <small>{item.foot}</small>
          </article>
        ))}
      </div>

      {isAdmin && showForm ? (
        <form className="card card-pad sop-rule-form" onSubmit={handleSubmit}>
          <div className="card-head">
            <div>
              <h3>{editingId ? "Chỉnh sửa quy tắc" : "Thêm quy tắc"}</h3>
              <p>Thông tin này sẽ lưu trực tiếp vào bảng operational.sop_rules</p>
            </div>
            <button className="button button-secondary button-small" onClick={() => setShowForm(false)} type="button">Đóng</button>
          </div>
          <div className="config-form-grid">
            <label><span>Mã quy tắc</span><input onChange={(event) => setForm((value) => ({ ...value, ruleCode: event.target.value }))} required value={form.ruleCode} /></label>
            <label><span>Tên quy tắc</span><input onChange={(event) => setForm((value) => ({
              ...value,
              actionConfigText: value.actionType === "CREATE_TASK" ? undefined : value.actionConfigText,
              ruleName: event.target.value
            }))} required value={form.ruleName} /></label>
            <label><span>Mức rủi ro</span><select onChange={(event) => setForm((value) => ({
              ...value,
              actionConfigText: value.actionType === "CREATE_TASK" ? undefined : value.actionConfigText,
              triggerRiskLevel: event.target.value as RiskLevel
            }))} value={form.triggerRiskLevel}>{riskOptions.map((item) => <option key={item} value={item}>{item}</option>)}</select></label>
            <label><span>Loại khu vực</span><select onChange={(event) => setForm((value) => ({ ...value, appliesToZoneType: event.target.value || null }))} value={form.appliesToZoneType ?? ""}>{zoneOptions.map((item) => <option key={item || "ALL"} value={item}>{item || "ALL"}</option>)}</select></label>
            <label><span>Action</span><select onChange={(event) => setForm((value) => ({
              ...value,
              actionConfigText: undefined,
              actionType: event.target.value as SopActionType,
              executionOrder: undefined
            }))} value={form.actionType}>{actionOptions.map((item) => <option key={item} value={item}>{item}</option>)}</select></label>
            <label className="wide-field"><span>Mô tả</span><input onChange={(event) => setForm((value) => ({ ...value, description: event.target.value }))} value={form.description ?? ""} /></label>
            <label className="checkbox-field"><input checked={form.isActive} onChange={(event) => setForm((value) => ({ ...value, isActive: event.target.checked }))} type="checkbox" /><span>Đang bật</span></label>
          </div>
          <div className="form-actions">
            <button className="btn primary" disabled={saving} type="submit">{editingId ? "Lưu thay đổi" : "Tạo quy tắc"}</button>
          </div>
        </form>
      ) : null}

      <div className="card toolbar sop-toolbar">
        <input className="input" onChange={(event) => setQuery(event.target.value)} placeholder="Tìm mã hoặc tên quy tắc" value={query} />
        <select className="select-input" onChange={(event) => setZoneFilter(event.target.value)} value={zoneFilter}>
          <option value="">Tất cả khu vực</option>
          {zoneOptions.filter(Boolean).map((item) => <option key={item} value={item}>{item}</option>)}
        </select>
      </div>

      {!selectedGroup ? (
        <section className="sop-risk-grid" aria-label="Nhóm quy tắc SOP theo mức rủi ro">
          {groupedRules.map((group) => (
            <article className="card card-pad sop-risk-card" key={group.riskLevel}>
              <div className="card-head">
                <div>
                  <h3>{group.riskLevel}</h3>
                  <p>{group.rules.length} quy tắc · {group.activeCount} đang bật</p>
                </div>
                <Badge tone={riskTone(group.riskLevel)}>{group.riskLevel}</Badge>
              </div>
              <div className="sop-risk-metrics">
                <span><strong>{group.topAction}</strong><small>action nổi bật</small></span>
              </div>
              <button className="button button-secondary" onClick={() => setSelectedRisk(group.riskLevel)} type="button">Chi tiết</button>
            </article>
          ))}
        </section>
      ) : (
        <article className="card card-pad">
          <div className="card-head">
            <div>
              <h3>Danh sách quy tắc {selectedGroup.riskLevel}</h3>
              <p>Hệ thống tự sắp xếp hành động khi nhiều quy tắc cùng khớp</p>
            </div>
            <div className="card-head-actions">
              <Badge tone={riskTone(selectedGroup.riskLevel)}>{selectedGroup.rules.length} quy tắc</Badge>
              <button className="button button-secondary button-small" onClick={() => setSelectedRisk(null)} type="button">Quay lại tổng quan</button>
            </div>
          </div>
          <div className="sop-rule-list">
            {selectedGroup.rules.length === 0 ? <div className="empty-state">Chưa có quy tắc cho mức {selectedGroup.riskLevel}.</div> : null}
            {selectedGroup.rules.map((rule) => (
              <div className="sop-rule-card" key={rule.id}>
                <div className="sop-main">
                  <h3>{rule.ruleName}</h3>
                  <div className="sop-meta">
                    <span className="code-chip">{rule.ruleCode}</span>
                    <Badge tone={riskTone(rule.triggerRiskLevel)}>{rule.triggerRiskLevel}</Badge>
                    <Badge tone="muted">{rule.appliesToZoneType ?? "ALL"}</Badge>
                    <Badge tone={rule.isActive ? "success" : "muted"}>{rule.isActive ? "Đang bật" : "Tắt"}</Badge>
                  </div>
                  <p>{rule.actionType}</p>
                </div>
                <div className="sop-stats">
                  {isAdmin ? (
                    <>
                      <button className="button button-secondary button-small" onClick={() => openEditForm(rule)} type="button">Chỉnh sửa</button>
                      <button className="button button-secondary button-small" onClick={() => handleDelete(rule)} type="button">Xóa</button>
                    </>
                  ) : <span className="readonly-chip">Chỉ xem</span>}
                </div>
              </div>
            ))}
          </div>
        </article>
      )}
    </section>
  );
}
