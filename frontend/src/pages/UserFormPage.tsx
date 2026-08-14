import { FormEvent, useEffect, useState } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import type { DemoUser } from "../App";
import { getPorts } from "../services/portService";
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
  currentUser?: DemoUser | null;
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
  portId: "",
  role: "OPERATOR",
  status: "ACTIVE"
};

const roleOptions: Array<{ label: string; value: UserRole }> = [
  { label: "ADMIN - System Administrator", value: "ADMIN" },
  { label: "PORT MANAGER - Port Operations Manager", value: "PORT_MANAGER" },
  { label: "OPERATOR - Port Operations Supervisor", value: "OPERATOR" }
];

const statusOptions: Array<{ label: string; value: UserStatus }> = [
  { label: "Hoạt động", value: "ACTIVE" },
  { label: "Tạm dừng", value: "INACTIVE" },
  { label: "Khóa", value: "LOCKED" }
];

function toNullablePortId(portId: string) {
  return portId.trim() ? portId : null;
}

function nextPortForRole(role: UserRole, currentPortId: string, fallbackPortId: string) {
  if (role === "ADMIN") {
    return "";
  }

  return currentPortId.trim() ? currentPortId : fallbackPortId;
}

function formFromUser(user: UserRecord, fallbackPortId: string): UserFormState {
  const role = user.role as UserRole;
  return {
    email: user.email,
    fullName: user.fullName,
    password: "",
    portId: role === "ADMIN" ? "" : user.portId ?? fallbackPortId,
    role,
    status: user.status
  };
}

function isEditingSignedInAdmin(
  currentUser: DemoUser | null | undefined,
  selectedUser: UserRecord | null,
  routeUserId: string | undefined
) {
  if (currentUser?.role !== "ADMIN" || selectedUser?.role !== "ADMIN") {
    return false;
  }

  const sameUserId = Boolean(currentUser.id && routeUserId && currentUser.id === routeUserId);
  const sameEmail = currentUser.email.trim().toLocaleLowerCase("en-US") === selectedUser.email.trim().toLocaleLowerCase("en-US");
  return sameUserId || sameEmail;
}

export function UserFormPage({ currentUser, mode }: UserFormPageProps) {
  const navigate = useNavigate();
  const { userId } = useParams();
  const [form, setForm] = useState<UserFormState>(emptyForm);
  const [selectedUser, setSelectedUser] = useState<UserRecord | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(mode === "edit");
  const [notFound, setNotFound] = useState(false);
  const [portOptions, setPortOptions] = useState<Array<{ label: string; value: string }>>([]);
  const [submitting, setSubmitting] = useState(false);

  useEffect(() => {
    let active = true;

    getPorts()
      .then((ports) => {
        if (!active) return;
        const options = ports.map((port) => ({ label: port.portName, value: port.portId }));
        setPortOptions(options);
        setForm((value) => {
          if (value.role === "ADMIN" || value.portId.trim() || options.length === 0) {
            return value;
          }

          return { ...value, portId: options[0].value };
        });
      })
      .catch((caught) => {
        if (!active) return;
        setError(caught instanceof Error ? caught.message : "Không thể tải danh sách cảng.");
      });

    return () => {
      active = false;
    };
  }, []);

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
        setSelectedUser(selectedUser);
        setForm((value) => formFromUser(selectedUser, value.portId));
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

    const isSelfAdminEdit = mode === "edit" && isEditingSignedInAdmin(currentUser, selectedUser, userId);
    const submittedRole: UserRole = isSelfAdminEdit ? "ADMIN" : form.role;

    try {
      const payload: UpdateUserInput = {
        email: form.email.trim(),
        fullName: form.fullName.trim(),
        portId: submittedRole === "ADMIN" ? null : toNullablePortId(form.portId),
        role: submittedRole,
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

  const isSelfAdminEdit = mode === "edit" && isEditingSignedInAdmin(currentUser, selectedUser, userId);

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
              aria-label="Vai trò"
              disabled={isSelfAdminEdit}
              onChange={(event) => {
                const role = event.target.value as UserRole;
                setForm((value) => ({
                  ...value,
                  portId: nextPortForRole(role, value.portId, portOptions[0]?.value ?? ""),
                  role
                }));
              }}
              value={form.role}
            >
              {roleOptions.map((option) => <option key={option.value} value={option.value}>{option.label}</option>)}
            </select>
            {isSelfAdminEdit ? <small className="field-hint">Admin không thể tự thay đổi vai trò của chính mình.</small> : null}
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
              <option disabled={form.role !== "ADMIN"} value="">Tất cả</option>
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
