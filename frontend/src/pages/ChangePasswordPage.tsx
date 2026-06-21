import { useState, type FormEvent } from "react";
import { Link } from "react-router-dom";
import { changePassword as changePasswordRequest } from "../services/authService";
import type { ChangePasswordInput } from "../types/auth";

type ChangePasswordPageProps = {
  changePassword?: (input: ChangePasswordInput) => Promise<void>;
  onChanged: () => void;
};

function strengthScore(password: string) {
  return [
    password.length >= 8,
    /[A-Z]/.test(password),
    /[a-z]/.test(password),
    /\d/.test(password),
    /[^A-Za-z0-9]/.test(password)
  ].filter(Boolean).length;
}

export function ChangePasswordPage({
  changePassword = changePasswordRequest,
  onChanged
}: ChangePasswordPageProps) {
  const [currentPassword, setCurrentPassword] = useState("");
  const [newPassword, setNewPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [error, setError] = useState("");
  const [submitting, setSubmitting] = useState(false);
  const score = strengthScore(newPassword);

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError("");
    if (score < 5) {
      setError("Mật khẩu chưa đáp ứng yêu cầu bảo mật.");
      return;
    }
    if (newPassword !== confirmPassword) {
      setError("Mật khẩu xác nhận không khớp.");
      return;
    }

    setSubmitting(true);
    try {
      await changePassword({ currentPassword, newPassword, confirmPassword });
      onChanged();
    } catch (requestError) {
      setError(requestError instanceof Error ? requestError.message : "Không thể đổi mật khẩu.");
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <section className="page-grid">
      <div className="section-heading">
        <div>
          <h2>Đổi mật khẩu</h2>
          <p>Sau khi đổi mật khẩu, tất cả phiên đăng nhập sẽ bị thu hồi</p>
        </div>
      </div>
      <form className="card password-form" onSubmit={handleSubmit}>
        <label htmlFor="current-password">Mật khẩu hiện tại</label>
        <input id="current-password" onChange={(event) => setCurrentPassword(event.target.value)} required type="password" value={currentPassword} />

        <label htmlFor="new-password">Mật khẩu mới</label>
        <input id="new-password" onChange={(event) => setNewPassword(event.target.value)} placeholder="Tối thiểu 8 ký tự" required type="password" value={newPassword} />
        <div className="password-strength"><span data-score={score} style={{ width: `${score * 20}%` }} /></div>
        <small>{score === 5 ? "Mật khẩu mạnh." : `Đã đáp ứng ${score}/5 yêu cầu.`}</small>

        <label htmlFor="confirm-password">Xác nhận mật khẩu mới</label>
        <input id="confirm-password" onChange={(event) => setConfirmPassword(event.target.value)} required type="password" value={confirmPassword} />

        {error ? <div className="form-error" role="alert">{error}</div> : null}
        <div className="password-actions">
          <Link className="button button-secondary" to="/profile">Hủy</Link>
          <button className="button button-primary" disabled={submitting} type="submit">
            {submitting ? "Đang xử lý..." : "Xác nhận thay đổi"}
          </button>
        </div>
      </form>
    </section>
  );
}
