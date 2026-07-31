import { useEffect, useMemo, useState } from "react";
import { Link, useParams } from "react-router-dom";
import type { DemoUser } from "../App";
import { Badge } from "../components/common/Badge";
import { acknowledgeAlert, getAlert, getAlertTasks } from "../services/alertService";
import {
  acknowledgeTask,
  assignTask,
  completeTask,
  getTaskAssignees,
  startTask,
  type TaskAssignee,
  type TaskLogRecord
} from "../services/taskService";
import type { AlertItem } from "../types/alert";
import { riskLabel } from "../utils/displayLabels";

type AlertDetailPageProps = { currentUser?: DemoUser };
type AssignmentFormState = { assignedUserId: string; dueAt: string; taskId: string };
type CompletionFormState = { completionNote: string; taskId: string };

function dueAtForInput(value?: string | null) {
  if (!value) return "";
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? "" : date.toISOString().slice(0, 16);
}

function badgeTone(level: string) {
  if (level === "CRITICAL") return "danger";
  if (level === "HIGH") return "warning";
  return "info";
}

function taskStatusLabel(status: string) {
  const labels: Record<string, string> = {
    NEW: "Chờ tiếp nhận",
    ACKNOWLEDGED: "Đã tiếp nhận",
    IN_PROGRESS: "Đang thực hiện",
    COMPLETED: "Đã hoàn tất",
    CANCELLED: "Đã hủy"
  };
  return labels[status] ?? status;
}

function recommendedAction(severity: string) {
  if (severity === "CRITICAL") return "Tạm dừng hoạt động tại khu vực, kiểm tra an toàn và thực hiện ngay quy trình ứng phó khẩn cấp.";
  if (severity === "HIGH") return "Hạn chế hoạt động tại khu vực, theo dõi sát diễn biến và thực hiện các nhiệm vụ ứng phó được giao.";
  return "Tiếp tục theo dõi diễn biến và chuẩn bị phương án ứng phó khi mức rủi ro tăng.";
}

export function AlertDetailPage({ currentUser }: AlertDetailPageProps) {
  const { alertId = "" } = useParams();
  const [alert, setAlert] = useState<AlertItem | null>(null);
  const [alertTasks, setAlertTasks] = useState<TaskLogRecord[]>([]);
  const [taskAssignees, setTaskAssignees] = useState<TaskAssignee[]>([]);
  const [loading, setLoading] = useState(true);
  const [actionMessage, setActionMessage] = useState<string | null>(null);
  const [assignmentForm, setAssignmentForm] = useState<AssignmentFormState | null>(null);
  const [completionForm, setCompletionForm] = useState<CompletionFormState | null>(null);
  const canAssign = !currentUser || currentUser.role === "ADMIN" || currentUser.role === "PORT_MANAGER";

  useEffect(() => {
    if (!alertId) return;
    setLoading(true);
    setActionMessage(null);
    void Promise.all([
      getAlert(alertId),
      getAlertTasks(alertId),
      canAssign ? getTaskAssignees().catch(() => []) : Promise.resolve([])
    ]).then(([alertResult, tasksResult, assigneesResult]) => {
      setAlert(alertResult);
      setAlertTasks(tasksResult);
      setTaskAssignees(assigneesResult);
    }).catch(() => {
      setAlert(null);
      setAlertTasks([]);
      setActionMessage("Không thể tải chi tiết cảnh báo hoặc bạn không có quyền xem cảnh báo này.");
    }).finally(() => setLoading(false));
  }, [alertId, canAssign]);

  const weatherFacts = useMemo(() => alert ? [
    { label: "Gió", value: alert.windSpeedMs != null ? `${alert.beaufortNumber != null ? `Cấp ${alert.beaufortNumber} · ` : ""}${alert.windSpeedMs} m/s` : "Chưa có dữ liệu" },
    { label: "Lượng mưa", value: alert.rainfall1hMm != null ? `${alert.rainfall1hMm} mm/giờ` : "Chưa có dữ liệu" },
    { label: "Tầm nhìn", value: alert.visibilityKm != null ? `${alert.visibilityKm} km` : "Chưa có dữ liệu" }
  ] : [], [alert]);

  function replaceTask(updatedTask: TaskLogRecord) {
    setAlertTasks((tasks) => tasks.map((task) => task.taskId === updatedTask.taskId ? updatedTask : task));
  }

  async function confirmAlert() {
    if (!alert) return;
    try {
      await acknowledgeAlert(alert.alertId);
      setAlert({ ...alert, acknowledged: true, read: true, status: "ACKNOWLEDGED" });
      setActionMessage("Bạn đã xác nhận tiếp nhận cảnh báo.");
    } catch { setActionMessage("Không thể xác nhận cảnh báo. Vui lòng thử lại."); }
  }

  async function saveAssignment() {
    if (!assignmentForm) return;
    if (!assignmentForm.assignedUserId) { setActionMessage("Vui lòng chọn người phụ trách."); return; }
    try {
      const updated = await assignTask(assignmentForm.taskId, {
        assignedUserId: assignmentForm.assignedUserId,
        dueAt: assignmentForm.dueAt ? new Date(assignmentForm.dueAt).toISOString() : null
      });
      replaceTask(updated);
      setAssignmentForm(null);
      setActionMessage("Đã phân công nhiệm vụ cho nhân sự cùng cảng.");
    } catch { setActionMessage("Không thể phân công. Hãy kiểm tra người được chọn có thuộc đúng cảng hay không."); }
  }

  async function runTaskAction(taskId: string, action: "acknowledge" | "start") {
    try {
      const updated = action === "acknowledge" ? await acknowledgeTask(taskId) : await startTask(taskId);
      replaceTask(updated);
      setActionMessage(action === "acknowledge" ? "Đã tiếp nhận nhiệm vụ." : "Đã bắt đầu thực hiện nhiệm vụ.");
    } catch { setActionMessage("Bạn không có quyền cập nhật nhiệm vụ này hoặc trạng thái không còn phù hợp."); }
  }

  async function saveCompletion() {
    if (!completionForm) return;
    if (completionForm.completionNote.trim().length < 10) {
      setActionMessage("Kết quả xử lý phải có ít nhất 10 ký tự.");
      return;
    }
    try {
      const updated = await completeTask(completionForm.taskId, { completionNote: completionForm.completionNote.trim() });
      replaceTask(updated);
      setCompletionForm(null);
      setActionMessage("Đã hoàn tất nhiệm vụ và lưu kết quả xử lý.");
    } catch { setActionMessage("Không thể hoàn tất nhiệm vụ. Vui lòng kiểm tra quyền xử lý."); }
  }

  return (
    <section className="page-grid alert-detail-page">
      <div className="section-heading alert-detail-heading">
        <div><span className="page-eyebrow">ĐIỀU PHỐI ỨNG PHÓ</span><h2>Chi tiết cảnh báo</h2><p>Nắm rõ tình huống, tiếp nhận cảnh báo và theo dõi công việc ứng phó.</p></div>
        <Link aria-label="Quay lại danh sách cảnh báo" className="button button-secondary button-small" to="/alerts">← Quay lại danh sách cảnh báo</Link>
      </div>

      {loading ? <div className="card alert-loading">Đang tải thông tin cảnh báo...</div> : null}
      {!loading && !alert ? <div className="card friendly-empty-state"><strong>Không tìm thấy cảnh báo</strong><small>Cảnh báo không tồn tại hoặc không thuộc phạm vi cảng bạn phụ trách.</small></div> : null}

      {!loading && alert ? (
        <article className={`card alert-overview risk-${alert.severity.toLowerCase()}`}>
          <header className="alert-overview-banner">
            <div className="alert-overview-icon" aria-hidden="true">!</div>
            <div className="alert-overview-title">
              <span className="alert-overview-eyebrow">CẢNH BÁO TẠI CẢNG</span>
              <h3>{alert.title}</h3>
              <div className="alert-location-chips"><span>{alert.portName}</span><span>{alert.zoneName}</span><time>{alert.createdAt}</time></div>
            </div>
            <div className="alert-overview-level"><span>Mức độ rủi ro</span><Badge tone={badgeTone(alert.severity)}>{riskLabel(alert.severity)}</Badge></div>
          </header>
          <div className="alert-overview-body">
            <section className="alert-summary-block"><span className="alert-section-label">Tình hình ghi nhận</span><p>{alert.message}</p></section>
            <div className="alert-facts" aria-label="Thông tin thời tiết">{weatherFacts.map((fact, index) => <div className="alert-fact" key={fact.label}><span className="alert-fact-number">0{index + 1}</span><div><span>{fact.label}</span><strong>{fact.value}</strong></div></div>)}</div>
            <div className="alert-action-panel"><div className="alert-action-icon" aria-hidden="true">!</div><div><strong>Việc cần thực hiện</strong><p>{recommendedAction(alert.severity)}</p></div>{!alert.acknowledged ? <button className="alert-confirm-button" onClick={() => void confirmAlert()} type="button">Xác nhận tiếp nhận</button> : <span className="alert-confirmed-badge">✓ Đã xác nhận</span>}</div>
          </div>
        </article>
      ) : null}

      {actionMessage ? <div className="inline-status alert-inline-status">{actionMessage}</div> : null}

      {!loading && alert ? (
        <section className="card alert-task-detail">
          <div className="table-card-head"><div><span className="page-eyebrow">KẾ HOẠCH ỨNG PHÓ</span><h3>Nhiệm vụ cần thực hiện</h3><p>Mỗi nhiệm vụ được liên kết trực tiếp với cảnh báo và chỉ giao cho nhân sự thuộc cùng cảng.</p></div><span className="task-count-pill">{alertTasks.length} nhiệm vụ</span></div>
          {alertTasks.length === 0 ? <div className="friendly-empty-state"><strong>Chưa có nhiệm vụ ứng phó</strong><small>Hệ thống chưa tìm thấy quy trình phù hợp với cảnh báo này.</small></div> : null}
          <div className="alert-task-list">{alertTasks.map((task) => (
            <article className={`alert-task-card task-${task.status.toLowerCase()}`} key={task.taskId}>
              <div className="task-card-main"><div className="task-card-topline"><Badge tone={badgeTone(task.priority)}>{riskLabel(task.priority)}</Badge><span className="task-status-pill">{taskStatusLabel(task.status)}</span></div><h4>{task.title}</h4><p>{task.description || "Thực hiện theo quy trình ứng phó đã được phê duyệt."}</p><div className="task-card-meta"><span><small>Người phụ trách</small><strong>{task.assignedUserName ?? "Chưa phân công"}</strong></span><span><small>Hạn xử lý</small><strong>{task.dueAt ? new Date(task.dueAt).toLocaleString("vi-VN") : "Chưa đặt thời hạn"}</strong></span></div>{task.completionNote ? <div className="task-card-result"><strong>Kết quả xử lý</strong><p>{task.completionNote}</p><small>Hoàn tất lúc {task.completedAt ? new Date(task.completedAt).toLocaleString("vi-VN") : "-"}</small></div> : null}</div>
              <div className="task-card-actions">
                {canAssign && task.status === "NEW" ? <button className="button button-secondary button-small" onClick={() => setAssignmentForm({ assignedUserId: task.assignedUserId ?? "", dueAt: dueAtForInput(task.dueAt), taskId: task.taskId })} type="button">Phân công</button> : null}
                {currentUser?.role === "OPERATOR" && task.assignedUserId === currentUser.id && task.status === "NEW" ? <button aria-label="Xác nhận" className="button button-primary button-small" onClick={() => void runTaskAction(task.taskId, "acknowledge")} type="button">Tiếp nhận</button> : null}
                {currentUser?.role === "OPERATOR" && task.assignedUserId === currentUser.id && task.status === "ACKNOWLEDGED" ? <button className="button button-primary button-small" onClick={() => void runTaskAction(task.taskId, "start")} type="button">Bắt đầu</button> : null}
                {currentUser?.role === "OPERATOR" && task.assignedUserId === currentUser.id && task.status === "IN_PROGRESS" ? <button className="button button-primary button-small" onClick={() => setCompletionForm({ completionNote: task.completionNote ?? "", taskId: task.taskId })} type="button">Hoàn tất</button> : null}
                <Link className="button button-secondary button-small" to={`/tasks/${task.taskId}`}>Xem nhiệm vụ</Link>
              </div>
            </article>
          ))}</div>
        </section>
      ) : null}

      {assignmentForm ? <div className="modal-backdrop"><section className="card alert-task-form modal-card" role="dialog" aria-label="Phân công nhiệm vụ"><div className="modal-card-head"><div><span className="page-eyebrow">PHÂN CÔNG NỘI BỘ</span><h3>Chọn người phụ trách</h3><p>Danh sách chỉ gồm Operator thuộc cảng của nhiệm vụ.</p></div><button aria-label="Đóng" className="modal-close" onClick={() => setAssignmentForm(null)} type="button">×</button></div><label><span>Người phụ trách</span><select className="select-input" onChange={(event) => setAssignmentForm((form) => form ? { ...form, assignedUserId: event.target.value } : form)} required value={assignmentForm.assignedUserId}><option value="">Chọn người phụ trách</option>{taskAssignees.filter((assignee) => !alert || !assignee.portId || assignee.portId === alert.portId).map((assignee) => <option key={assignee.userId} value={assignee.userId}>{assignee.fullName} · {assignee.email}</option>)}</select></label><label><span>Hạn xử lý</span><input className="input" onChange={(event) => setAssignmentForm((form) => form ? { ...form, dueAt: event.target.value } : form)} type="datetime-local" value={assignmentForm.dueAt} /></label><div className="form-actions"><button className="button button-primary" onClick={() => void saveAssignment()} type="button">Lưu phân công</button><button className="button button-secondary" onClick={() => setAssignmentForm(null)} type="button">Hủy</button></div></section></div> : null}

      {completionForm ? <div className="modal-backdrop"><section className="card alert-task-form modal-card" role="dialog" aria-label="Hoàn tất nhiệm vụ"><div className="modal-card-head"><div><span className="page-eyebrow">KẾT QUẢ XỬ LÝ</span><h3>Hoàn tất nhiệm vụ</h3><p>Ghi lại kết quả để phục vụ nhật ký vận hành và báo cáo.</p></div><button aria-label="Đóng" className="modal-close" onClick={() => setCompletionForm(null)} type="button">×</button></div><label><span>Ghi chú hoàn tất</span><textarea className="input" onChange={(event) => setCompletionForm((form) => form ? { ...form, completionNote: event.target.value } : form)} placeholder="Ví dụ: Đã tạm dừng khu vực và hoàn tất kiểm tra an toàn..." rows={4} value={completionForm.completionNote} /></label><div className="form-actions"><button aria-label="Lưu hoàn tất" className="button button-primary" onClick={() => void saveCompletion()} type="button">Lưu kết quả</button><button className="button button-secondary" onClick={() => setCompletionForm(null)} type="button">Hủy</button></div></section></div> : null}
    </section>
  );
}
