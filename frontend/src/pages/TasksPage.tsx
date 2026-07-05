import { useEffect, useMemo, useState } from "react";
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

const PAGE_SIZE = 15;
const riskOptions = ["LOW", "MEDIUM", "HIGH", "CRITICAL"];

function uniqueBy<T>(items: T[], keyOf: (item: T) => string) {
  const seen = new Set<string>();
  return items.filter((item) => {
    const key = keyOf(item);
    if (seen.has(key)) return false;
    seen.add(key);
    return true;
  });
}

function taskDateValue(task: TaskLogRecord) {
  return task.createdAt.slice(0, 10);
}

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
  const [currentPage, setCurrentPage] = useState(1);
  const [selectedPortId, setSelectedPortId] = useState("");
  const [selectedZoneName, setSelectedZoneName] = useState("");
  const [selectedPriority, setSelectedPriority] = useState("");
  const [fromDate, setFromDate] = useState("");
  const [toDate, setToDate] = useState("");
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

  const portOptions = useMemo(
    () => uniqueBy(tasks, (task) => task.portId).map((task) => ({
      portId: task.portId,
      label: `${task.portCode} - ${task.portName}`
    })),
    [tasks]
  );
  const zoneOptions = useMemo(() => {
    const scopedTasks = selectedPortId ? tasks.filter((task) => task.portId === selectedPortId) : tasks;
    return [...new Set(scopedTasks.map((task) => zoneLabel(task)).filter(Boolean))].sort();
  }, [selectedPortId, tasks]);

  useEffect(() => {
    if (selectedZoneName && !zoneOptions.includes(selectedZoneName)) {
      setSelectedZoneName("");
    }
  }, [selectedZoneName, zoneOptions]);

  const filteredTasks = useMemo(() => tasks.filter((task) => {
    const createdDate = taskDateValue(task);

    return (!selectedPortId || task.portId === selectedPortId)
      && (!selectedZoneName || zoneLabel(task) === selectedZoneName)
      && (!selectedPriority || task.priority === selectedPriority)
      && (!fromDate || createdDate >= fromDate)
      && (!toDate || createdDate <= toDate);
  }), [fromDate, selectedPortId, selectedPriority, selectedZoneName, tasks, toDate]);

  const totalPages = Math.max(1, Math.ceil(filteredTasks.length / PAGE_SIZE));
  const visibleTasks = useMemo(() => {
    const start = (currentPage - 1) * PAGE_SIZE;
    return filteredTasks.slice(start, start + PAGE_SIZE);
  }, [currentPage, filteredTasks]);

  useEffect(() => {
    setCurrentPage((page) => Math.min(page, totalPages));
  }, [totalPages]);

  useEffect(() => {
    setCurrentPage(1);
  }, [fromDate, selectedPortId, selectedPriority, selectedZoneName, toDate]);

  function resetFilters() {
    setSelectedPortId("");
    setSelectedZoneName("");
    setSelectedPriority("");
    setFromDate("");
    setToDate("");
  }

  return (
    <section className="page-grid">
      <div className="section-heading">
        <div>
          <h2>Nhật ký nhiệm vụ</h2>
          <p>Theo dõi nhiệm vụ được tạo từ quy tắc SOP và kết quả vận hành thực tế.</p>
        </div>
      </div>

      {error ? <div className="alert alert-error">{error}</div> : null}

      <div className="card toolbar sop-toolbar filter-toolbar">
        <label>
          <span>Cảng</span>
          <select className="select-input" onChange={(event) => setSelectedPortId(event.target.value)} value={selectedPortId}>
            <option value="">Tất cả cảng</option>
            {portOptions.map((port) => (
              <option key={port.portId} value={port.portId}>{port.label}</option>
            ))}
          </select>
        </label>
        <label>
          <span>Khu vực</span>
          <select className="select-input" onChange={(event) => setSelectedZoneName(event.target.value)} value={selectedZoneName}>
            <option value="">Tất cả khu vực</option>
            {zoneOptions.map((zone) => (
              <option key={zone} value={zone}>{zone}</option>
            ))}
          </select>
        </label>
        <label>
          <span>Từ ngày</span>
          <input className="input" onChange={(event) => setFromDate(event.target.value)} type="date" value={fromDate} />
        </label>
        <label>
          <span>Đến ngày</span>
          <input className="input" onChange={(event) => setToDate(event.target.value)} type="date" value={toDate} />
        </label>
        <label>
          <span>Cấp độ rủi ro</span>
          <select className="select-input" onChange={(event) => setSelectedPriority(event.target.value)} value={selectedPriority}>
            <option value="">Tất cả cấp độ</option>
            {riskOptions.map((risk) => (
              <option key={risk} value={risk}>{risk}</option>
            ))}
          </select>
        </label>
        <button className="button button-secondary button-small" onClick={resetFilters} type="button">Xóa lọc</button>
      </div>

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

            {!isLoading && filteredTasks.length === 0 ? (
              <tr>
                <td colSpan={8}>Chưa có nhiệm vụ nào.</td>
              </tr>
            ) : null}

            {!isLoading
              ? visibleTasks.map((task) => (
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
        {!isLoading && totalPages > 1 ? (
          <div className="table-pagination" aria-label="Phân trang nhật ký nhiệm vụ">
            <button
              className="button button-secondary button-small"
              disabled={currentPage === 1}
              onClick={() => setCurrentPage((page) => Math.max(1, page - 1))}
              type="button"
            >
              Trước
            </button>
            <span>Trang {currentPage}/{totalPages}</span>
            <button
              className="button button-secondary button-small"
              disabled={currentPage === totalPages}
              onClick={() => setCurrentPage((page) => Math.min(totalPages, page + 1))}
              type="button"
            >
              Sau
            </button>
          </div>
        ) : null}
      </article>
    </section>
  );
}
