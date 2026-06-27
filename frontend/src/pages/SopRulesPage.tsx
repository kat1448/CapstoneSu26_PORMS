import { FormEvent, useEffect, useMemo, useState } from "react";
import { Badge } from "../components/common/Badge";
import {
  createSopRule,
  deleteSopRule,
  getSopRules,
  updateSopRule,
  type SopActionType,
  type SopRule,
  type SopRuleInput,
  type SopRulesResponse
} from "../services/sopRulesService";
import type { RiskLevel } from "../services/riskConfigService";

const riskOptions: RiskLevel[] = ["MEDIUM", "HIGH", "CRITICAL"];
const zoneOptions = ["", "DOCK", "YARD", "GATE", "WAREHOUSE"];
const actionOptions: SopActionType[] = ["CREATE_TASK", "SEND_ALERT", "RESTRICT_ZONE", "SET_LIMITED_MODE", "STOP_OPERATIONS"];

const emptyForm: SopRuleInput = {
  actionConfigText: "{}",
  actionType: "CREATE_TASK",
  appliesToZoneType: null,
  changeReason: "Cập nhật từ màn hình SOP",
  description: "",
  executionOrder: 100,
  isActive: true,
  previousRiskLevel: null,
  ruleCode: "",
  ruleName: "",
  triggerRiskLevel: "HIGH"
};

function riskTone(risk: string): "danger" | "info" | "warning" {
  if (risk === "CRITICAL") return "danger";
  if (risk === "HIGH") return "warning";
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

export function SopRulesPage() {
  const [data, setData] = useState<SopRulesResponse | null>(null);
  const [form, setForm] = useState<SopRuleInput>(emptyForm);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [query, setQuery] = useState("");
  const [riskFilter, setRiskFilter] = useState("");
  const [zoneFilter, setZoneFilter] = useState("");
  const [showForm, setShowForm] = useState(false);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
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

  const filteredRules = useMemo(() => {
    const normalized = query.trim().toLowerCase();
    return (data?.rules ?? []).filter((rule) => {
      const textMatched = !normalized
        || rule.ruleCode.toLowerCase().includes(normalized)
        || rule.ruleName.toLowerCase().includes(normalized);
      const riskMatched = !riskFilter || rule.triggerRiskLevel === riskFilter;
      const zoneMatched = !zoneFilter || rule.appliesToZoneType === zoneFilter;
      return textMatched && riskMatched && zoneMatched;
    });
  }, [data?.rules, query, riskFilter, zoneFilter]);

  function openCreateForm() {
    setEditingId(null);
    setForm(emptyForm);
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

  if (loading) {
    return <section className="page-grid"><div className="card card-pad">Đang tải quy tắc SOP...</div></section>;
  }

  const summary = data?.summary ?? { activeRules: 0, automatedTasks: 0, recentExecutions: 0, totalRules: 0 };
  const kpis = [
    { label: "Tổng quy tắc", value: summary.totalRules, foot: "Quy tắc trong database" },
    { label: "Đang bật", value: summary.activeRules, foot: "Sẵn sàng kích hoạt" },
    { label: "Lần kích hoạt", value: summary.recentExecutions, foot: "30 ngày gần nhất" },
    { label: "Nhiệm vụ tự động", value: summary.automatedTasks, foot: "Action CREATE_TASK" }
  ];

  return (
    <section className="page-grid sop-page">
      <div className="section-heading">
        <div>
          <h2>Quy tắc SOP</h2>
          <p>Quản lý quy tắc tự động kích hoạt hành động vận hành theo mức rủi ro</p>
        </div>
        <div className="head-actions">
          <button className="button button-secondary" onClick={reload} type="button">Tải lại</button>
          <button className="button button-primary" onClick={openCreateForm} type="button">Thêm quy tắc</button>
        </div>
      </div>

      {error ? <div className="form-error" role="alert">{error}</div> : null}

      <div className="stats-grid">
        {kpis.map((item) => (
          <article className="card card-pad stat-card" key={item.label}>
            <span>{item.label}</span>
            <strong>{item.value}</strong>
            <small>{item.foot}</small>
          </article>
        ))}
      </div>

      {showForm ? (
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
            <label><span>Tên quy tắc</span><input onChange={(event) => setForm((value) => ({ ...value, ruleName: event.target.value }))} required value={form.ruleName} /></label>
            <label><span>Mức rủi ro</span><select onChange={(event) => setForm((value) => ({ ...value, triggerRiskLevel: event.target.value as RiskLevel }))} value={form.triggerRiskLevel}>{riskOptions.map((item) => <option key={item} value={item}>{item}</option>)}</select></label>
            <label><span>Loại khu vực</span><select onChange={(event) => setForm((value) => ({ ...value, appliesToZoneType: event.target.value || null }))} value={form.appliesToZoneType ?? ""}>{zoneOptions.map((item) => <option key={item || "ALL"} value={item}>{item || "ALL"}</option>)}</select></label>
            <label><span>Action</span><select onChange={(event) => setForm((value) => ({ ...value, actionType: event.target.value as SopActionType }))} value={form.actionType}>{actionOptions.map((item) => <option key={item} value={item}>{item}</option>)}</select></label>
            <label><span>Thứ tự</span><input min={0} onChange={(event) => setForm((value) => ({ ...value, executionOrder: Number(event.target.value) }))} type="number" value={form.executionOrder} /></label>
            <label className="wide-field"><span>Mô tả</span><input onChange={(event) => setForm((value) => ({ ...value, description: event.target.value }))} value={form.description ?? ""} /></label>
            <label className="wide-field"><span>Action config JSON</span><textarea onChange={(event) => setForm((value) => ({ ...value, actionConfigText: event.target.value }))} rows={4} value={form.actionConfigText} /></label>
            <label className="checkbox-field"><input checked={form.isActive} onChange={(event) => setForm((value) => ({ ...value, isActive: event.target.checked }))} type="checkbox" /><span>Đang bật</span></label>
          </div>
          <div className="form-actions">
            <button className="btn primary" disabled={saving} type="submit">{editingId ? "Lưu thay đổi" : "Tạo quy tắc"}</button>
          </div>
        </form>
      ) : null}

      <div className="sop-layout-grid">
        <div className="page-grid">
          <div className="card toolbar sop-toolbar">
            <input className="input" onChange={(event) => setQuery(event.target.value)} placeholder="Tìm mã hoặc tên quy tắc" value={query} />
            <select className="select-input" onChange={(event) => setRiskFilter(event.target.value)} value={riskFilter}>
              <option value="">Tất cả mức rủi ro</option>
              {riskOptions.map((item) => <option key={item} value={item}>{item}</option>)}
            </select>
            <select className="select-input" onChange={(event) => setZoneFilter(event.target.value)} value={zoneFilter}>
              <option value="">Tất cả khu vực</option>
              {zoneOptions.filter(Boolean).map((item) => <option key={item} value={item}>{item}</option>)}
            </select>
          </div>

          <article className="card card-pad">
            <div className="card-head">
              <div>
                <h3>Danh sách quy tắc</h3>
                <p>Thứ tự ưu tiên thấp hơn sẽ chạy trước khi nhiều quy tắc cùng khớp</p>
              </div>
              <Badge tone="info">{summary.activeRules} active</Badge>
            </div>
            <div className="sop-rule-list">
              {filteredRules.map((rule) => (
                <div className="sop-rule-card" key={rule.id}>
                  <div className="sop-order">{rule.executionOrder}</div>
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
                    <strong>{rule.executionCount}</strong>
                    <small>lần chạy</small>
                    <button className="button button-secondary button-small" onClick={() => openEditForm(rule)} type="button">Chỉnh sửa</button>
                    <button className="button button-secondary button-small" onClick={() => handleDelete(rule)} type="button">Xóa</button>
                  </div>
                </div>
              ))}
            </div>
          </article>
        </div>

        <aside className="card card-pad sop-timeline-panel">
          <div className="card-head">
            <div>
              <h3>Kích hoạt gần đây</h3>
              <p>Audit nhanh các SOP đã chạy</p>
            </div>
          </div>
          <div className="timeline-list">
            {(data?.executions ?? []).map((item) => (
              <div className={`timeline-entry risk-${item.riskLevel.toLowerCase()}`} key={item.id}>
                <div>
                  <strong>{item.ruleCode}</strong>
                  <p>{item.ruleName} · {item.actionType}{item.zoneName ? ` · ${item.zoneName}` : ""}</p>
                </div>
                <span>{item.status}</span>
              </div>
            ))}
            {data?.executions.length === 0 ? <div className="empty-state">Chưa có lần kích hoạt SOP.</div> : null}
          </div>
        </aside>
      </div>
    </section>
  );
}
