import type { DemoUser } from "../App";

type ProfilePageProps = {
  currentUser: DemoUser;
};

export function ProfilePage({ currentUser }: ProfilePageProps) {
  return (
    <section className="page-grid">
      <div className="section-heading">
        <div>
          <h2>Thông tin cá nhân</h2>
          <p>Quản lý thông tin tài khoản demo</p>
        </div>
      </div>
      <article className="card form-card">
        <div className="profile-grid">
          <div>
            <label>Họ và tên</label>
            <input defaultValue={currentUser.name} readOnly />
          </div>
          <div>
            <label>Email</label>
            <input defaultValue={currentUser.email} readOnly />
          </div>
          <div>
            <label>Vai trò</label>
            <input defaultValue={currentUser.role} readOnly />
          </div>
          <div>
            <label>Đơn vị</label>
            <input defaultValue={currentUser.portName} readOnly />
          </div>
        </div>
      </article>
    </section>
  );
}
