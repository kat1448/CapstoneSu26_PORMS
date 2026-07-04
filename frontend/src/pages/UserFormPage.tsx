import { FormEvent, useEffect, useState } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import {
  createUser,
  getUsers,
  updateUser,
  type CreateUserInput,
  type UpdateUserInput,
  type UserRecord,
  type UserRole,
  type UserStatus
} from "../services/userService";

type UserFormPageProps = {
  mode: "create" | "edit";
};

type UserFormState = {
  email: string;
  fullName: string;
  password: string;
  portId: string;
  role: UserRole;
  status: UserStatus;
};

const emptyForm: UserFormState = {
  email: "",
  fullName: "",
  password: "",
  portId: "port-dntsa",
  role: "STANDARD_USER",
  status: "ACTIVE"
};

const roleOptions: Array<{ label: string; value: UserRole }> = [
  { label: "Super Admin", value: "SUPER_ADMIN" },
  { label: "Admin", value: "ADMIN" },
  { label: "Standard User", value: "STANDARD_USER" }
];

const statusOptions: Array<{ label: string; value: UserStatus }> = [
  { label: "Hoạt động", value: "ACTIVE" },
  { label: "Tạm dừng", value: "INACTIVE" },
  { label: "Khóa", value: "LOCKED" }
];

const portOptions = [
  { label: "Tất cả", value: "" },
  { label: "Cảng Tiên Sa", value: "port-dntsa" },
  { label: "Cảng Liên Chiểu", value: "port-lien-chieu" },
  { label: "Cảng Chân Mây", value: "port-chan-may" }
];

function toNullablePortId(portId: string) {
  return portId.trim() ? portId : null;
}

function nextPortForRole(role: UserRole, currentPortId: string) {
  if (role === "SUPER_ADMIN") {
    return "";
  }

  return currentPortId.trim() ? currentPortId : "port-dntsa";
}

function formFromUser(user: UserRecord): UserFormState {
  const role = user.role as UserRole;
  return {
    email: user.email,
    fullName: user.fullName,
    password: "",
    portId: role === "SUPER_ADMIN" ? "" : user.portId ?? "port-dntsa",
    role,
    status: user.status
  };
}

export function UserFormPage({ mode }: UserFormPageProps) {
  const navigate = useNavigate();
  const { userId } = useParams();
  const [form, setForm] = useState<UserFormState>(emptyForm);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(mode === "edit");
  const [notFound, setNotFound] = useState(false);
  const [submitting, setSubmitting] = useState(false);

  useEffect(() => {
    if (mode !== "edit") {
      return;
    }

    let active = true;
    setLoading(true);
    setNotFound(false);

    getUsers()
      .then((users) => {
        if (!active) return;
        const selectedUser = users.find((user) => user.userId === userId);
        if (!selectedUser) {
          setNotFound(true);
          return;
        }
        setForm(formFromUser(selectedUser));
      })
      .catch((caught) => {
        if (!active) return;
        setError(caught instanceof Error ? caught.message : "Không thể tải người dùng.");
      })
      .finally(() => {
        if (active) {
          setLoading(false);
        }
      });

    return () => {
      active = false;
    };
  }, [mode, userId]);

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError(null);
    setSubmitting(true);

    try {
      const payload: UpdateUserInput = {
        email: form.email.trim(),
        fullName: form.fullName.trim(),
        portId: toNullablePortId(form.portId),
        role: form.role,
        status: form.status
      };

      if (mode === "edit") {
        if (!userId) {
          throw new Error("Thiếu mã người dùng.");
        }
        await updateUser(userId, payload);
      } else {
        const createPayload: CreateUserInput = {
          ...payload,
          password: form.password
        };
        await createUser(createPayload);
      }

      navigate("/users");
    } catch (caught) {
      setError(caught instanceof Error ? caught.message : "Không thể lưu người dùng.");
    } finally {
      setSubmitting(false);
    }
  }

  if (loading) {
    return (
      <section className="page-grid">
        <div className="card card-pad">Đang tải người dùng...</div>
      </section>
    );
  }

  if (notFound) {
    return (
      <section className="page-grid">
        <div className="card card-pad">
          <div className="card-head">
            <div>
              <h3>Không tìm thấy người dùng</h3>
              <p>Người dùng này không còn tồn tại trong hệ thống.</p>
            </div>
            <Link className="btn ghost" to="/users">Quay lại</Link>
          </div>
        </div>
      </section>
    );
  }

  return (
    <section className="page-grid">
      <div className="section-heading">
        <div>
          <h2>{mode === "edit" ? "Chỉnh sửa người dùng" : "Tạo người dùng"}</h2>
          <p>{mode === "edit" ? "Cập nhật thông tin tài khoản đã chọn" : "Tạo tài khoản vận hành mới"}</p>
        </div>
        <Link className="btn ghost" to="/users">Quay lại</Link>
      </div>

      <form className="card card-pad user-form-card" onSubmit={handleSubmit}>
        <div className="user-form-grid">
          <label>
            <span>Họ tên</span>
            <input
              onChange={(event) => setForm((value) => ({ ...value, fullName: event.target.value }))}
              required
              value={form.fullName}
            />
          </label>
          <label>
            <span>Email</span>
            <input
              onChange={(event) => setForm((value) => ({ ...value, email: event.target.value }))}
              required
              type="email"
              value={form.email}
            />
          </label>
          <label>
            <span>Vai trò</span>
            <select
              onChange={(event) => {
                const role = event.target.value as UserRole;
                setForm((value) => ({ ...value, portId: nextPortForRole(role, value.portId), role }));
              }}
              value={form.role}
            >
              {roleOptions.map((option) => <option key={option.value} value={option.value}>{option.label}</option>)}
            </select>
          </label>
          <label>
            <span>Trạng thái</span>
            <select
              onChange={(event) => setForm((value) => ({ ...value, status: event.target.value as UserStatus }))}
              value={form.status}
            >
              {statusOptions.map((option) => <option key={option.value} value={option.value}>{option.label}</option>)}
            </select>
          </label>
          <label>
            <span>Cảng phụ trách</span>
            <select
              onChange={(event) => setForm((value) => ({ ...value, portId: event.target.value }))}
              value={form.portId}
            >
              {portOptions.map((option) => <option key={option.value} value={option.value}>{option.label}</option>)}
            </select>
          </label>
          {mode === "create" ? (
            <label>
              <span>Mật khẩu</span>
              <input
                minLength={8}
                onChange={(event) => setForm((value) => ({ ...value, password: event.target.value }))}
                required
                type="password"
                value={form.password}
              />
            </label>
          ) : null}
        </div>

        {error ? <div className="form-error" role="alert">{error}</div> : null}

        <div className="form-actions">
          <button className="btn primary" disabled={submitting} type="submit">
            {mode === "edit" ? "Lưu thay đổi" : "Tạo người dùng"}
          </button>
        </div>
      </form>
    </section>
  );
}
