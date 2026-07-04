import { useEffect, useState } from "react";
import { Badge } from "../components/common/Badge";
import { formatTimeLabel } from "../services/api";
import { getTasks, type TaskLogRecord } from "../services/taskService";

const statusLabels: Record<string, string> = {
  ACKNOWLEDGED: "Đã xác nhận",
  CANCELLED: "Đã hủy",
  COMPLETED: "Hoàn tất",
  IN_PROGRESS: "Đang thực hiện",
  NEW: "Mới"
};

function priorityTone(priority: string) {
  if (priority === "CRITICAL") return "danger";
  if (priority === "HIGH") return "warning";
  if (priority === "LOW") return "muted";
  return "info";
}

function assigneeLabel(task: TaskLogRecord) {
  return task.assignedUserName ?? task.assignedTeam ?? "Chưa phân công";
}

function zoneLabel(task: TaskLogRecord) {
  return task.zoneName ?? task.portName ?? "Toàn cảng";
}

export function TasksPage() {
  const [tasks, setTasks] = useState<TaskLogRecord[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let isActive = true;

    async function loadTasks() {
      try {
        setIsLoading(true);
        setError(null);
        const result = await getTasks();
        if (isActive) {
          setTasks(result);
        }
      } catch (loadError) {
        if (isActive) {
          setError(loadError instanceof Error ? loadError.message : "Không thể tải nhật ký nhiệm vụ.");
        }
      } finally {
        if (isActive) {
          setIsLoading(false);
        }
      }
    }

    void loadTasks();

    return () => {
      isActive = false;
    };
  }, []);

  return (
    <section className="page-grid">
      <div className="section-heading">
        <div>
          <h2>Nhật ký nhiệm vụ</h2>
          <p>Theo dõi nhiệm vụ được tạo từ quy tắc SOP và kết quả vận hành thực tế.</p>
        </div>
      </div>

      {error ? <div className="alert alert-error">{error}</div> : null}

      <article className="card table-card">
        <table className="data-table">
          <thead>
            <tr>
              <th>Mã</th>
              <th>Nhiệm vụ</th>
              <th>Cảng</th>
              <th>Khu vực</th>
              <th>Ưu tiên</th>
              <th>Phụ trách</th>
              <th>Trạng thái</th>
              <th>Thời gian</th>
            </tr>
          </thead>
          <tbody>
            {isLoading ? (
              <tr>
                <td colSpan={8}>Đang tải nhiệm vụ...</td>
              </tr>
            ) : null}

            {!isLoading && tasks.length === 0 ? (
              <tr>
                <td colSpan={8}>Chưa có nhiệm vụ nào.</td>
              </tr>
            ) : null}

            {!isLoading
              ? tasks.map((task) => (
                  <tr key={task.taskId}>
                    <td><span className="code-chip">{task.taskCode}</span></td>
                    <td>
                      <strong>{task.title}</strong>
                      {task.isSimulation ? <span className="meta-text">Mô phỏng</span> : null}
                    </td>
                    <td>
                      <strong>{task.portCode}</strong>
                      <span className="meta-text">{task.portName}</span>
                    </td>
                    <td>{zoneLabel(task)}</td>
                    <td><Badge tone={priorityTone(task.priority)}>{task.priority}</Badge></td>
                    <td>{assigneeLabel(task)}</td>
                    <td>{statusLabels[task.status] ?? task.status}</td>
                    <td>{formatTimeLabel(task.createdAt)}</td>
                  </tr>
                ))
              : null}
          </tbody>
        </table>
      </article>
    </section>
  );
}
