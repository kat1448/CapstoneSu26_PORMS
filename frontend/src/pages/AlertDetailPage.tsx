import { useEffect, useState } from "react";
import { Link, useParams } from "react-router-dom";
import { Badge } from "../components/common/Badge";
import { getAlert, getAlertTasks } from "../services/alertService";
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

type AssignmentFormState = {
  assignedUserId: string;
  dueAt: string;
  taskId: string;
};

type CompletionFormState = {
  completionNote: string;
  taskId: string;
};

function dueAtForInput(value?: string | null) {
  if (!value) return "";
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return "";
  return date.toISOString().slice(0, 16);
}

function badgeTone(level: string) {
  if (level === "CRITICAL") return "danger";
  if (level === "HIGH") return "warning";
  return "info";
}

export function AlertDetailPage() {
  const { alertId = "" } = useParams();
  const [alert, setAlert] = useState<AlertItem | null>(null);
  const [alertTasks, setAlertTasks] = useState<TaskLogRecord[]>([]);
  const [taskAssignees, setTaskAssignees] = useState<TaskAssignee[]>([]);
  const [loading, setLoading] = useState(true);
  const [actionMessage, setActionMessage] = useState<string | null>(null);
  const [assignmentForm, setAssignmentForm] = useState<AssignmentFormState | null>(null);
  const [completionForm, setCompletionForm] = useState<CompletionFormState | null>(null);

  useEffect(() => {
    if (!alertId) return;

    setLoading(true);
    setActionMessage(null);
    void Promise.all([
      getAlert(alertId),
      getAlertTasks(alertId),
      getTaskAssignees().catch(() => [])
    ])
      .then(([alertResult, tasksResult, assigneesResult]) => {
        setAlert(alertResult);
        setAlertTasks(tasksResult);
        setTaskAssignees(assigneesResult);
      })
      .catch(() => {
        setAlert(null);
        setAlertTasks([]);
        setActionMessage("Không tải được chi tiết cảnh báo.");
      })
      .finally(() => setLoading(false));
  }, [alertId]);

  function replaceTask(updatedTask: TaskLogRecord) {
    setAlertTasks((tasks) => tasks.map((task) => (
      task.taskId === updatedTask.taskId ? updatedTask : task
    )));
  }

  async function saveAssignment() {
    if (!assignmentForm) return;

    if (!assignmentForm.assignedUserId) {
      setActionMessage("Vui lòng chọn người phụ trách.");
      return;
    }

    try {
      const updatedTask = await assignTask(assignmentForm.taskId, {
        assignedUserId: assignmentForm.assignedUserId,
        dueAt: assignmentForm.dueAt ? new Date(assignmentForm.dueAt).toISOString() : null
      });
      replaceTask(updatedTask);
      setAssignmentForm(null);
      setActionMessage("Đã lưu phân công nhiệm vụ.");
    } catch {
      setActionMessage("Lưu phân công thất bại.");
    }
  }

  async function runTaskAction(taskId: string, action: "acknowledge" | "start") {
    try {
      const updatedTask = action === "acknowledge"
        ? await acknowledgeTask(taskId)
        : await startTask(taskId);
      replaceTask(updatedTask);
      setActionMessage(action === "acknowledge" ? "Đã xác nhận nhiệm vụ." : "Đã bắt đầu thực hiện nhiệm vụ.");
    } catch {
      setActionMessage("Cập nhật trạng thái nhiệm vụ thất bại.");
    }
  }

  async function saveCompletion() {
    if (!completionForm) return;

    try {
      const updatedTask = await completeTask(completionForm.taskId, {
        completionNote: completionForm.completionNote.trim()
      });
      replaceTask(updatedTask);
      setCompletionForm(null);
      setActionMessage("Đã hoàn tất nhiệm vụ.");
    } catch {
      setActionMessage("Hoàn tất nhiệm vụ thất bại.");
    }
  }

  return (
    <section className="page-grid alert-detail-page">
      <div className="section-heading">
        <div>
          <h2>Chi tiết cảnh báo</h2>
          <p>Theo dõi nhiệm vụ cần thực hiện cho cảnh báo đã kích hoạt</p>
        </div>
        <Link className="button button-secondary button-small" to="/alerts">
          Quay lại danh sách cảnh báo
        </Link>
      </div>

      {loading ? <div className="card alert-task-detail">Đang tải chi tiết cảnh báo...</div> : null}

      {!loading && alert ? (
        <div className="card alert-task-detail">
          <div className="table-card-head">
            <div>
              <h3>{alert.title}</h3>
              <p>{alert.portCode} - {alert.portName} | {alert.zoneName} | {alert.createdAt}</p>
            </div>
            <Badge tone={badgeTone(alert.severity)}>{alert.severity}</Badge>
          </div>
          <p>{alert.message}</p>
        </div>
      ) : null}

      {actionMessage ? <div className="inline-status">{actionMessage}</div> : null}

      {!loading ? (
        <div className="card table-card alert-task-detail">
          <div className="table-card-head">
            <div>
              <h3>Nhiệm vụ cần thực hiện</h3>
              <p>Phân công người phụ trách và cập nhật trạng thái xử lý cho cảnh báo này.</p>
            </div>
          </div>
          {alertTasks.length === 0 ? <p>Chưa có nhiệm vụ cho cảnh báo này.</p> : null}
          {alertTasks.length > 0 ? (
            <table className="data-table">
              <thead>
                <tr>
                  <th>Mã</th>
                  <th>Nhiệm vụ</th>
                  <th>Ưu tiên</th>
                  <th>Trạng thái</th>
                  <th>Người phụ trách</th>
                  <th>Hạn xử lý</th>
                  <th>Thao tác</th>
                </tr>
              </thead>
              <tbody>
                {alertTasks.map((task) => (
                  <tr key={task.taskId}>
                    <td>{task.taskCode}</td>
                    <td>
                      <strong>{task.title}</strong>
                      <p>{task.description}</p>
                    </td>
                    <td>
                      <Badge tone={badgeTone(task.priority)}>{task.priority}</Badge>
                    </td>
                    <td>{task.status}</td>
                    <td>{task.assignedUserName ?? "Chưa phân công"}</td>
                    <td>{task.dueAt ? new Date(task.dueAt).toLocaleString("vi-VN") : "Chưa đặt"}</td>
                    <td>
                      <div className="table-actions">
                        <button
                          className="button button-secondary button-small"
                          onClick={() => setAssignmentForm({
                            assignedUserId: task.assignedUserId ?? "",
                            dueAt: dueAtForInput(task.dueAt),
                            taskId: task.taskId
                          })}
                          type="button"
                        >
                          Phân công
                        </button>
                        {task.status === "NEW" ? (
                          <button className="button button-primary button-small" onClick={() => void runTaskAction(task.taskId, "acknowledge")} type="button">
                            Xác nhận
                          </button>
                        ) : null}
                        {task.status === "ACKNOWLEDGED" || task.status === "NEW" ? (
                          <button className="button button-primary button-small" onClick={() => void runTaskAction(task.taskId, "start")} type="button">
                            Bắt đầu
                          </button>
                        ) : null}
                        {task.status !== "COMPLETED" ? (
                          <button className="button button-primary button-small" onClick={() => setCompletionForm({ completionNote: task.completionNote ?? "", taskId: task.taskId })} type="button">
                            Hoàn tất
                          </button>
                        ) : null}
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          ) : null}
        </div>
      ) : null}

      {assignmentForm ? (
        <div className="card alert-task-form">
          <h3>Phân công nhiệm vụ</h3>
          <label>
            <span>Người phụ trách</span>
            <select
              className="select-input"
              onChange={(event) => setAssignmentForm((form) => form ? { ...form, assignedUserId: event.target.value } : form)}
              required
              value={assignmentForm.assignedUserId}
            >
              <option value="">Chọn người phụ trách</option>
              {taskAssignees.map((assignee) => (
                <option key={assignee.userId} value={assignee.userId}>
                  {assignee.fullName} - {assignee.email}
                </option>
              ))}
            </select>
          </label>
          <label>
            <span>Hạn xử lý</span>
            <input
              className="input"
              onChange={(event) => setAssignmentForm((form) => form ? { ...form, dueAt: event.target.value } : form)}
              type="datetime-local"
              value={assignmentForm.dueAt}
            />
          </label>
          <div className="form-actions">
            <button className="button button-primary" onClick={() => void saveAssignment()} type="button">Lưu phân công</button>
            <button className="button button-secondary" onClick={() => setAssignmentForm(null)} type="button">Hủy</button>
          </div>
        </div>
      ) : null}

      {completionForm ? (
        <div className="card alert-task-form">
          <h3>Hoàn tất nhiệm vụ</h3>
          <label>
            <span>Ghi chú hoàn tất</span>
            <textarea
              className="input"
              onChange={(event) => setCompletionForm((form) => form ? { ...form, completionNote: event.target.value } : form)}
              rows={4}
              value={completionForm.completionNote}
            />
          </label>
          <div className="form-actions">
            <button className="button button-primary" onClick={() => void saveCompletion()} type="button">Lưu hoàn tất</button>
            <button className="button button-secondary" onClick={() => setCompletionForm(null)} type="button">Hủy</button>
          </div>
        </div>
      ) : null}
    </section>
  );
}
