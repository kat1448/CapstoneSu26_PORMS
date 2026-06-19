import { Badge } from "../components/common/Badge";

const tasks = [
  {
    assignee: "Phạm Minh Đức",
    code: "TASK-2026-041",
    createdAt: "21:54",
    priority: "HIGH",
    status: "Đang thực hiện",
    title: "Hạn chế bốc xếp tại Bến số 1",
    zone: "Bến số 1"
  },
  {
    assignee: "Đội vận hành",
    code: "TASK-2026-040",
    createdAt: "21:53",
    priority: "MEDIUM",
    status: "Chờ xác nhận",
    title: "Neo giữ container tại Bãi container A",
    zone: "Bãi container A"
  },
  {
    assignee: "Trực ca cổng",
    code: "TASK-2026-039",
    createdAt: "21:51",
    priority: "MEDIUM",
    status: "Hoàn tất",
    title: "Giảm tốc độ phương tiện tại Cổng chính",
    zone: "Cổng chính"
  }
] as const;

export function TasksPage() {
  return (
    <section className="page-grid">
      <div className="section-heading">
        <div>
          <h2>Nhật ký nhiệm vụ</h2>
          <p>Theo dõi nhiệm vụ được tạo tự động từ các quy tắc SOP</p>
        </div>
      </div>
      <article className="card table-card">
        <table className="data-table">
          <thead>
            <tr>
              <th>Mã</th><th>Nhiệm vụ</th><th>Khu vực</th><th>Ưu tiên</th>
              <th>Phụ trách</th><th>Trạng thái</th><th>Thời gian</th>
            </tr>
          </thead>
          <tbody>
            {tasks.map((task) => (
              <tr key={task.code}>
                <td><span className="code-chip">{task.code}</span></td>
                <td><strong>{task.title}</strong></td>
                <td>{task.zone}</td>
                <td><Badge tone={task.priority === "HIGH" ? "warning" : "info"}>{task.priority}</Badge></td>
                <td>{task.assignee}</td>
                <td>{task.status}</td>
                <td>{task.createdAt}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </article>
    </section>
  );
}
