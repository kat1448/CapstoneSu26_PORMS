import { useEffect, useMemo, useState } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import type { DemoUser } from "../App";
import { Badge } from "../components/common/Badge";
import {
  acknowledgeTask,
  assignTask,
  completeTask,
  getTask,
  getTaskAssignees,
  startTask,
  type TaskAssignee,
  type TaskLogRecord
} from "../services/taskService";
import { riskLabel } from "../utils/displayLabels";

type TaskDetailPageProps = { currentUser: DemoUser };

const statusLabels: Record<string, string> = {
  NEW: "Chờ tiếp nhận",
  ACKNOWLEDGED: "Đã tiếp nhận",
  IN_PROGRESS: "Đang thực hiện",
  COMPLETED: "Đã hoàn tất",
  CANCELLED: "Đã hủy"
};

function taskStatusLabel(task: TaskLogRecord) {
  if (task.status === "NEW") {
    return task.assignedUserId ? "Chờ tiếp nhận" : "Chờ phân công";
  }
  return statusLabels[task.status] ?? task.status;
}

function formatDate(value?: string | null) {
  if (!value) return "Chưa ghi nhận";
  return new Date(value).toLocaleString("vi-VN");
}

function datetimeInputValue(value?: string | null) {
  if (!value) return "";
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? "" : date.toISOString().slice(0, 16);
}

function tone(priority: string) {
  if (priority === "CRITICAL") return "danger";
  if (priority === "HIGH") return "warning";
  return priority === "LOW" ? "muted" : "info";
}

export function TaskDetailPage({ currentUser }: TaskDetailPageProps) {
  const { taskId = "" } = useParams();
  const navigate = useNavigate();
  const [task, setTask] = useState<TaskLogRecord | null>(null);
  const [assignees, setAssignees] = useState<TaskAssignee[]>([]);
  const [selectedAssignee, setSelectedAssignee] = useState("");
  const [dueAt, setDueAt] = useState("");
  const [completionNote, setCompletionNote] = useState("");
  const [showAssignment, setShowAssignment] = useState(false);
  const [showCompletion, setShowCompletion] = useState(false);
  const [message, setMessage] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const canAssign = currentUser.role === "ADMIN" || currentUser.role === "PORT_MANAGER";

  useEffect(() => {
    if (!taskId) return;
    setLoading(true);
    void Promise.all([
      getTask(taskId),
      canAssign ? getTaskAssignees().catch(() => []) : Promise.resolve([])
    ]).then(([taskResult, assigneeResult]) => {
      setTask(taskResult);
      setAssignees(assigneeResult);
      setSelectedAssignee(taskResult.assignedUserId ?? "");
      setDueAt(datetimeInputValue(taskResult.dueAt));
      setCompletionNote(taskResult.completionNote ?? "");
    }).catch(() => setTask(null)).finally(() => setLoading(false));
  }, [canAssign, taskId]);

  const isAssignedOperator = currentUser.role === "OPERATOR" && task?.assignedUserId === currentUser.id;
  const timeline = useMemo(() => task ? [
    { complete: true, label: "Nhiệm vụ được tạo", time: formatDate(task.createdAt) },
    { complete: Boolean(task.assignedUserId), label: task.assignedUserName ? `Đã giao cho ${task.assignedUserName}` : "Chờ phân công", time: task.assignedUserId ? "Đã ghi nhận" : "Chưa thực hiện" },
    { complete: Boolean(task.acknowledgedAt), label: "Operator đã tiếp nhận", time: formatDate(task.acknowledgedAt) },
    { complete: Boolean(task.startedAt), label: "Bắt đầu thực hiện", time: formatDate(task.startedAt) },
    { complete: Boolean(task.completedAt), label: "Hoàn tất nhiệm vụ", time: formatDate(task.completedAt) }
  ] : [], [task]);

  async function runAction(action: "acknowledge" | "start") {
    if (!task) return;
    setSaving(true);
    setMessage(null);
    try {
      const updated = action === "acknowledge"
        ? await acknowledgeTask(task.taskId)
        : await startTask(task.taskId);
      setTask(updated);
      setMessage(action === "acknowledge" ? "Bạn đã tiếp nhận nhiệm vụ." : "Nhiệm vụ đã chuyển sang trạng thái đang thực hiện.");
    } catch (error) {
      setMessage(error instanceof Error ? error.message : "Không thể cập nhật nhiệm vụ.");
    } finally {
      setSaving(false);
    }
  }

  async function saveAssignment() {
    if (!task || !selectedAssignee) {
      setMessage("Vui lòng chọn Operator phụ trách.");
      return;
    }
    setSaving(true);
    try {
      const updated = await assignTask(task.taskId, {
        assignedUserId: selectedAssignee,
        dueAt: dueAt ? new Date(dueAt).toISOString() : null
      });
      setTask(updated);
      setShowAssignment(false);
      setMessage("Đã phân công nhiệm vụ và gửi email cho Operator.");
    } catch (error) {
      setMessage(error instanceof Error ? error.message : "Không thể phân công nhiệm vụ.");
    } finally {
      setSaving(false);
    }
  }

  async function saveCompletion() {
    if (!task) return;
    const result = completionNote.trim();
    if (result.length < 10) {
      setMessage("Kết quả xử lý phải có ít nhất 10 ký tự.");
      return;
    }
    setSaving(true);
    try {
      const updated = await completeTask(task.taskId, { completionNote: result });
      setTask(updated);
      setShowCompletion(false);
      setMessage("Đã hoàn tất nhiệm vụ và lưu kết quả xử lý.");
    } catch (error) {
      setMessage(error instanceof Error ? error.message : "Không thể hoàn tất nhiệm vụ.");
    } finally {
      setSaving(false);
    }
  }

  if (loading) return <section className="card task-detail-loading">Đang tải nhiệm vụ...</section>;
  if (!task) return <section className="card friendly-empty-state"><strong>Không tìm thấy nhiệm vụ</strong><small>Nhiệm vụ không tồn tại hoặc không thuộc phạm vi của bạn.</small><button className="button button-secondary" onClick={() => navigate("/tasks")} type="button">Quay lại danh sách</button></section>;

  return (
    <section className="page-grid task-detail-page">
      <header className="task-detail-hero">
        <div>
          <span className="page-eyebrow">ĐIỀU PHỐI NHIỆM VỤ</span>
          <h2>{task.title}</h2>
          <p>{task.portName} · {task.zoneName ?? "Toàn cảng"}</p>
        </div>
        <div className="task-detail-hero-actions">
          <Badge tone={tone(task.priority)}>{riskLabel(task.priority)}</Badge>
          <span className={`task-state task-state-${task.status.toLowerCase()}`}>{taskStatusLabel(task)}</span>
          <Link className="button button-secondary button-small" to="/tasks">← Danh sách nhiệm vụ</Link>
        </div>
      </header>

      {message ? <div className="inline-status">{message}</div> : null}

      <div className="task-detail-grid">
        <article className="card task-detail-main">
          <div className="task-detail-section">
            <span className="page-eyebrow">YÊU CẦU THỰC HIỆN</span>
            <p className="task-detail-description">{task.description || "Thực hiện theo quy trình ứng phó đã được phê duyệt."}</p>
          </div>
          <div className="task-detail-facts">
            <div><span>Mã nhiệm vụ</span><strong>{task.taskCode}</strong></div>
            <div><span>Người phụ trách</span><strong>{task.assignedUserName ?? "Chưa phân công"}</strong></div>
            <div><span>Hạn xử lý</span><strong>{formatDate(task.dueAt)}</strong></div>
            <div><span>Nguồn dữ liệu</span><strong>{task.isSimulation ? "Kịch bản mô phỏng" : "Vận hành thực tế"}</strong></div>
          </div>

          {task.completionNote ? (
            <section className="task-result-panel">
              <div><span className="page-eyebrow">KẾT QUẢ XỬ LÝ</span><h3>Nhiệm vụ đã hoàn tất</h3></div>
              <p>{task.completionNote}</p>
              <footer><span>Người thực hiện: <strong>{task.assignedUserName}</strong></span><span>Hoàn tất lúc: <strong>{formatDate(task.completedAt)}</strong></span></footer>
            </section>
          ) : null}

          <div className="task-detail-actions">
            {canAssign && task.status === "NEW" ? <button className="button button-secondary" onClick={() => setShowAssignment(true)} type="button">{task.assignedUserId ? "Đổi người phụ trách" : "Phân công Operator"}</button> : null}
            {isAssignedOperator && task.status === "NEW" ? <button className="button button-primary" disabled={saving} onClick={() => void runAction("acknowledge")} type="button">Tiếp nhận nhiệm vụ</button> : null}
            {isAssignedOperator && task.status === "ACKNOWLEDGED" ? <button className="button button-primary" disabled={saving} onClick={() => void runAction("start")} type="button">Bắt đầu thực hiện</button> : null}
            {isAssignedOperator && task.status === "IN_PROGRESS" ? <button className="button button-primary" onClick={() => setShowCompletion(true)} type="button">Hoàn tất và ghi kết quả</button> : null}
            {task.alertId ? <Link className="button button-secondary" to={`/alerts/${task.alertId}`}>Xem cảnh báo liên quan</Link> : null}
          </div>
        </article>

        <aside className="card task-timeline-card">
          <span className="page-eyebrow">TIẾN ĐỘ XỬ LÝ</span>
          <h3>Dòng thời gian nhiệm vụ</h3>
          <div className="task-timeline">{timeline.map((item) => <div className={item.complete ? "is-complete" : ""} key={item.label}><span className="task-timeline-dot">{item.complete ? "✓" : ""}</span><div><strong>{item.label}</strong><small>{item.time}</small></div></div>)}</div>
        </aside>
      </div>

      {showAssignment ? <div className="modal-backdrop"><section className="card modal-card" role="dialog" aria-label="Phân công nhiệm vụ"><div className="modal-card-head"><div><span className="page-eyebrow">PHÂN CÔNG NỘI BỘ</span><h3>Chọn người phụ trách</h3><p>Chỉ Operator thuộc đúng cảng mới xuất hiện trong danh sách.</p></div><button aria-label="Đóng" className="modal-close" onClick={() => setShowAssignment(false)} type="button">×</button></div><label><span>Operator phụ trách</span><select className="select-input" onChange={(event) => setSelectedAssignee(event.target.value)} value={selectedAssignee}><option value="">Chọn Operator</option>{assignees.filter((item) => item.portId === task.portId).map((item) => <option key={item.userId} value={item.userId}>{item.fullName} · {item.email}</option>)}</select></label><label><span>Hạn xử lý</span><input className="input" onChange={(event) => setDueAt(event.target.value)} type="datetime-local" value={dueAt} /></label><div className="form-actions"><button className="button button-primary" disabled={saving} onClick={() => void saveAssignment()} type="button">Lưu và gửi email</button><button className="button button-secondary" onClick={() => setShowAssignment(false)} type="button">Hủy</button></div></section></div> : null}

      {showCompletion ? <div className="modal-backdrop"><section className="card modal-card" role="dialog" aria-label="Hoàn tất nhiệm vụ"><div className="modal-card-head"><div><span className="page-eyebrow">KẾT QUẢ XỬ LÝ</span><h3>Hoàn tất nhiệm vụ</h3><p>Ghi rõ công việc đã thực hiện và tình trạng khu vực sau xử lý.</p></div><button aria-label="Đóng" className="modal-close" onClick={() => setShowCompletion(false)} type="button">×</button></div><label><span>Kết quả xử lý <strong>*</strong></span><textarea className="input" onChange={(event) => setCompletionNote(event.target.value)} placeholder="Ví dụ: Đã tạm dừng khu vực, kiểm tra thiết bị và xác nhận khu vực an toàn..." rows={6} value={completionNote} /></label><small className="field-hint">Tối thiểu 10 ký tự. Nội dung sẽ được lưu trong lịch sử vận hành.</small><div className="form-actions"><button className="button button-primary" disabled={saving || completionNote.trim().length < 10} onClick={() => void saveCompletion()} type="button">Xác nhận hoàn tất</button><button className="button button-secondary" onClick={() => setShowCompletion(false)} type="button">Hủy</button></div></section></div> : null}
    </section>
  );
}
