import type { DemoUser } from "../App";
import { useState, type FormEvent } from "react";

type LoginPageProps = {
  demoUsers: DemoUser[];
  onLogin: (email: string, password: string) => Promise<void>;
};

export function LoginPage({ demoUsers, onLogin }: LoginPageProps) {
  const [email, setEmail] = useState("admin@porms.vn");
  const [password, setPassword] = useState("Admin@2026!");
  const [passwordVisible, setPasswordVisible] = useState(false);
  const [error, setError] = useState("");
  const [submitting, setSubmitting] = useState(false);

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setSubmitting(true);
    setError("");
    try {
      await onLogin(email, password);
    } catch (requestError) {
      setError(requestError instanceof Error ? requestError.message : "Đăng nhập không thành công.");
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <div className="login-page">
      <section className="login-brand-panel">
        <div className="brand-lockup">
          <div className="brand-mark large">P</div>
          <div>
            <strong>PORMS</strong>
            <small>Port Operation Risk Management System</small>
          </div>
        </div>
        <h1>Vận hành cảng an toàn hơn với dữ liệu thời gian thực.</h1>
        <p>
          Hệ thống hỗ trợ quyết định, đánh giá rủi ro thời tiết và tự động kích hoạt quy
          trình vận hành cho Cảng Tiên Sa, Đà Nẵng.
        </p>
        <div className="flow-line">
          <span>Thời tiết</span>
          <b>→</b>
          <span>Risk Engine</span>
          <b>→</b>
          <span>SOP</span>
          <b>→</b>
          <span>Cảnh báo & nhiệm vụ</span>
        </div>
      </section>
      <section className="login-form-panel">
        <div className="login-card">
          <h2>Đăng nhập PORMS</h2>
          <p>Hệ thống quản lý rủi ro vận hành cảng biển</p>
          <form className="login-form" onSubmit={handleSubmit}>
            <label className="field-label" htmlFor="login-email">
              Địa chỉ email
            </label>
            <input
              className="input"
              id="login-email"
              onChange={(event) => setEmail(event.target.value)}
              required
              type="email"
              value={email}
            />
            <label className="field-label" htmlFor="login-password">
              Mật khẩu
            </label>
            <div className="password-wrap">
              <input
                className="input"
                defaultValue="Admin@123"
                id="login-password"
                onChange={(event) => setPassword(event.target.value)}
                required
                type={passwordVisible ? "text" : "password"}
                value={password}
              />
              <button
                aria-label="Hiện mật khẩu"
                className="password-toggle"
                onClick={() => setPasswordVisible((value) => !value)}
                type="button"
              >
                👁
              </button>
            </div>
            {error ? <div className="form-error" role="alert">{error}</div> : null}
            <button className="button button-primary login-submit" disabled={submitting} type="submit">
              {submitting ? "Đang đăng nhập..." : "Đăng nhập"}
            </button>
          </form>
          <div className="demo-title">Tài khoản demo nhanh</div>
          <div className="demo-account-list">
            {demoUsers.map((user) => (
              <button
                className="demo-account"
                key={user.role}
                onClick={() => {
                  setEmail(user.email);
                  setPassword("Admin@2026!");
                }}
                type="button"
              >
                <span className="avatar large">{user.initials}</span>
                <span>
                  <strong>{user.name}</strong>
                  <small>
                    {user.email} · {user.role}
                  </small>
                </span>
              </button>
            ))}
          </div>
          <p className="login-note">
            Bản trình diễn ngoại tuyến · Dữ liệu được mô phỏng cho mục đích demo
          </p>
        </div>
      </section>
    </div>
  );
}
