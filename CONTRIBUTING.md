# PORMS — Cấu trúc Dự án & Git Workflow

> **Port Operation Risk Management System**  
> Tài liệu dành cho toàn bộ thành viên nhóm. Đọc kỹ trước khi bắt đầu code.

---

## 1. Cấu trúc Thư mục (Repository Structure)

```
PORMS/                                        # Root của monorepo
│
├── .github/                                  # Cấu hình GitHub
│   ├── workflows/
│   │   ├── ci-backend.yml                    # CI: build + test ASP.NET khi push lên dev/main
│   │   ├── ci-frontend.yml                   # CI: lint + build React khi push lên dev/main
│   │   └── deploy.yml                        # CD: deploy lên EC2 khi merge vào main
│   └── PULL_REQUEST_TEMPLATE.md              # Template bắt buộc khi tạo PR
│
├── docs/                                     # Tài liệu dự án (không phải code)
│   ├── PORMS_Project_Overview.docx           # Overview + kiến trúc tổng thể
│   ├── api-contracts/                        # Swagger YAML chốt trước khi code (T1)
│   │   ├── auth-zone.yaml                    # Owner: BE-B — Auth, Port, Zone endpoints
│   │   ├── weather-risk.yaml                 # Owner: BE-C — Weather, Risk endpoints
│   │   ├── sop-alert.yaml                    # Owner: BE-D — SOP, Alert, Mode endpoints
│   │   └── etl-simulation.yaml              # Owner: PM/DE — Simulation, ETL status
│   └── database/
│       ├── schema.sql                        # Full DDL: cả hai schema operational + analytics
│       └── erd.png                           # Entity Relationship Diagram (export từ DBeaver)
│
├── backend/                                  # ASP.NET Core 8 — REST API chính
│   │
│   ├── PORMS.sln                             # Solution file — mở bằng Visual Studio / Rider
│   │
│   ├── PORMS.API/                            # Tầng trình bày: Controllers, Middleware, DI config
│   │   ├── Controllers/
│   │   │   ├── AuthController.cs             # BE-B: POST /auth/login, /auth/register, /auth/refresh
│   │   │   ├── PortController.cs             # BE-B: CRUD /ports
│   │   │   ├── ZoneController.cs             # BE-B: CRUD /zones, /zones/{id}/restrict
│   │   │   ├── UserController.cs             # BE-B: User management (Admin only)
│   │   │   ├── WeatherController.cs          # BE-C: GET /weather/current, /weather/history
│   │   │   ├── RiskController.cs             # BE-C: GET /risk/current, /risk/history, config CRUD
│   │   │   ├── SopController.cs              # BE-D: CRUD /sop-rules, GET /sop/executions
│   │   │   ├── OperationModeController.cs    # BE-D: GET /mode/current, POST /mode/override
│   │   │   ├── TaskController.cs             # BE-D: GET /tasks, /tasks/{id}
│   │   │   ├── AlertController.cs            # BE-D: GET /alerts, PATCH /alerts/{id}/read
│   │   │   ├── OperationLogController.cs     # BE-D: GET /operation-logs (filter by date/zone/type)
│   │   │   └── SimulationController.cs       # BE-C: POST /simulation/start, /simulation/stop
│   │   │
│   │   ├── Middleware/
│   │   │   ├── JwtMiddleware.cs              # BE-B: Validate JWT, inject claims vào HttpContext
│   │   │   ├── RbacMiddleware.cs             # BE-B: Kiểm tra role (Admin/CompanyAdmin/Operator)
│   │   │   └── ErrorHandlingMiddleware.cs    # Global exception handler, trả về chuẩn ProblemDetails
│   │   │
│   │   ├── BackgroundServices/
│   │   │   └── RiskEvaluationService.cs      # BE-C: IHostedService — trigger Risk Engine sau ETL
│   │   │
│   │   ├── Program.cs                        # Entry point — DI registration, middleware pipeline
│   │   ├── appsettings.json                  # Config mẫu (không commit secret)
│   │   └── appsettings.Development.json      # Config local dev (đã .gitignore)
│   │
│   ├── PORMS.Domain/                         # Tầng domain thuần — không phụ thuộc framework nào
│   │   ├── Entities/
│   │   │   ├── Port.cs                       # BE-B
│   │   │   ├── Zone.cs                       # BE-B
│   │   │   ├── User.cs                       # BE-B
│   │   │   ├── WeatherReading.cs             # BE-C
│   │   │   ├── RiskAssessment.cs             # BE-C
│   │   │   ├── SopRule.cs                    # BE-D
│   │   │   ├── OperationModeLog.cs           # BE-D
│   │   │   ├── TaskLog.cs                    # BE-D
│   │   │   ├── Alert.cs                      # BE-D
│   │   │   └── OperationEvent.cs             # BE-D (audit log tổng hợp)
│   │   ├── Enums/
│   │   │   ├── RiskLevel.cs                  # LOW / MEDIUM / HIGH / CRITICAL
│   │   │   ├── OperationMode.cs              # NORMAL / LIMITED / STOP
│   │   │   ├── ZoneType.cs                   # DOCK / YARD / GATE
│   │   │   └── UserRole.cs                   # Admin / CompanyAdmin / Operator
│   │   └── Events/
│   │       └── RiskChangedEvent.cs           # BE-C publish → BE-D subscribe (in-process event)
│   │
│   ├── PORMS.Application/                    # Tầng nghiệp vụ — Services, DTOs, Interfaces
│   │   ├── Services/
│   │   │   ├── Auth/
│   │   │   │   ├── IAuthService.cs
│   │   │   │   └── AuthService.cs            # BE-B: JWT generation, password hash
│   │   │   ├── Port/
│   │   │   │   ├── IPortService.cs
│   │   │   │   └── PortService.cs            # BE-B
│   │   │   ├── Zone/
│   │   │   │   ├── IZoneService.cs
│   │   │   │   └── ZoneService.cs            # BE-B: CRUD + restriction logic
│   │   │   ├── Weather/
│   │   │   │   ├── IWeatherService.cs
│   │   │   │   ├── WeatherService.cs         # BE-C: OpenWeather API client, normalize m/s→Beaufort
│   │   │   │   └── OpenWeatherClient.cs      # BE-C: HTTP client wrapper, retry policy
│   │   │   ├── Risk/
│   │   │   │   ├── IRiskEngine.cs
│   │   │   │   └── RiskEngine.cs             # BE-C: Beaufort + rainfall evaluation, worst-case agg
│   │   │   ├── Sop/
│   │   │   │   ├── ISopEngine.cs
│   │   │   │   └── SopEngine.cs              # BE-D: Subscribe RiskChangedEvent, lookup sop_rules
│   │   │   ├── Mode/
│   │   │   │   ├── IOperationModeService.cs
│   │   │   │   └── OperationModeService.cs   # BE-D: State machine NORMAL→LIMITED→STOP
│   │   │   ├── Task/
│   │   │   │   └── TaskGeneratorService.cs   # BE-D: Auto-tạo task_log khi SOP execute
│   │   │   ├── Alert/
│   │   │   │   └── AlertService.cs           # BE-D: Tạo alert, mark-read
│   │   │   └── Simulation/
│   │   │       └── SimulationService.cs      # BE-C: Replay historical weather, speed_multiplier
│   │   └── DTOs/                             # Request/Response objects (không expose Entity trực tiếp)
│   │       ├── Auth/
│   │       ├── Port/
│   │       ├── Weather/
│   │       ├── Risk/
│   │       ├── Sop/
│   │       └── Alert/
│   │
│   ├── PORMS.Infrastructure/                 # Tầng hạ tầng: DB, external API
│   │   ├── Data/
│   │   │   ├── ApplicationDbContext.cs       # EF Core DbContext, cấu hình relations
│   │   │   ├── Configurations/               # IEntityTypeConfiguration cho từng entity
│   │   │   └── Migrations/                   # EF Core migrations — KHÔNG sửa tay
│   │   └── Repositories/                     # Nếu cần custom query ngoài EF
│   │
│   └── PORMS.Tests/                          # Unit test + Integration test
│       ├── Unit/
│       │   ├── RiskEngineTests.cs            # BE-C: test từng ngưỡng Beaufort/rainfall
│       │   ├── SopEngineTests.cs             # BE-D: test rule lookup, SopExecutor
│       │   └── OperationModeTests.cs         # BE-D: test state machine transitions
│       └── Integration/
│           └── WeatherRiskChainTests.cs      # Test chain: mock weather → risk → sop → mode
│
├── frontend/                                 # React 18 + TypeScript + Vite
│   │
│   ├── src/
│   │   ├── components/                       # UI components tái sử dụng
│   │   │   ├── common/                       # Button, Input, Modal, Badge, Spinner...
│   │   │   ├── layout/                       # Sidebar, Navbar, PageWrapper
│   │   │   ├── dashboard/
│   │   │   │   ├── WeatherCard.tsx           # Hiển thị weather hiện tại (wind, rain, temp)
│   │   │   │   ├── RiskBadge.tsx             # Badge đổi màu theo RiskLevel (LOW=xanh...CRITICAL=đỏ)
│   │   │   │   ├── ModeIndicator.tsx         # Hiển thị NORMAL/LIMITED/STOP + timestamp
│   │   │   │   ├── AlertBell.tsx             # Icon chuông + badge số lượng unread
│   │   │   │   └── RiskTrendChart.tsx        # Chart.js line chart — risk 24h gần nhất
│   │   │   ├── port/
│   │   │   │   ├── PortCard.tsx
│   │   │   │   ├── ZoneList.tsx
│   │   │   │   └── MapPicker.tsx             # Leaflet.js — chọn tọa độ port/zone
│   │   │   └── alert/
│   │   │       ├── AlertPanel.tsx            # Danh sách alert unread, click để mark-read
│   │   │       └── LogViewer.tsx             # Bảng operation_events với filter
│   │   │
│   │   ├── pages/                            # Route-level components (1 file = 1 trang)
│   │   │   ├── LoginPage.tsx
│   │   │   ├── DashboardPage.tsx             # Trang chính: weather + risk + mode + alert
│   │   │   ├── PortManagementPage.tsx        # CRUD cảng
│   │   │   ├── ZonePage.tsx                  # CRUD vùng + restriction banner
│   │   │   ├── AlertPage.tsx                 # Danh sách alert đầy đủ
│   │   │   ├── LogPage.tsx                   # Operation log viewer
│   │   │   ├── AdminPage.tsx                 # User management (Admin only)
│   │   │   ├── RiskConfigPage.tsx            # Chỉnh ngưỡng Beaufort/rainfall (Admin)
│   │   │   ├── SopConfigPage.tsx             # Quản lý SOP rules (Admin)
│   │   │   └── BiDashboardPage.tsx           # Metabase iframe embed
│   │   │
│   │   ├── services/                         # Axios API calls — 1 file per domain
│   │   │   ├── api.ts                        # Axios instance, base URL, JWT interceptor
│   │   │   ├── authService.ts
│   │   │   ├── portService.ts
│   │   │   ├── weatherService.ts
│   │   │   ├── riskService.ts
│   │   │   ├── alertService.ts               # Polling logic GET /alerts mỗi 30 giây
│   │   │   └── simulationService.ts
│   │   │
│   │   ├── hooks/                            # Custom React hooks
│   │   │   ├── useAuth.ts                    # Lấy user info từ JWT, check role
│   │   │   ├── usePolling.ts                 # Generic hook cho alert polling
│   │   │   └── useRiskColor.ts               # Map RiskLevel → Tailwind color class
│   │   │
│   │   ├── types/                            # TypeScript interfaces — mirror DTOs từ BE
│   │   │   ├── auth.types.ts
│   │   │   ├── port.types.ts
│   │   │   ├── weather.types.ts
│   │   │   ├── risk.types.ts
│   │   │   └── alert.types.ts
│   │   │
│   │   ├── utils/                            # Helper functions thuần
│   │   │   ├── beaufortFormatter.ts          # Format Beaufort number → mô tả tiếng Việt
│   │   │   └── dateFormatter.ts              # Format UTC → UTC+7 hiển thị
│   │   │
│   │   ├── App.tsx                           # Router setup, ProtectedRoute
│   │   └── main.tsx                          # Entry point, mount React app
│   │
│   ├── public/
│   │   └── logo.svg
│   ├── index.html
│   ├── package.json
│   ├── tsconfig.json
│   └── vite.config.ts
│
├── etl/                                      # Python 3.11 + Prefect 2.x — ETL Pipeline
│   │
│   ├── flows/
│   │   ├── weather_collector.py              # PM/DE: Gọi OpenWeather API mỗi 15 phút/port
│   │   ├── weather_transformer.py            # PM/DE: Normalize m/s→Beaufort, chuẩn hóa UTC+7
│   │   └── dw_loader.py                      # PM/DE: Load vào fact_weather_readings (DW schema)
│   │
│   ├── models/
│   │   └── weather.py                        # Pydantic models cho raw + normalized weather data
│   │
│   ├── utils/
│   │   ├── db.py                             # SQLAlchemy engine, session factory cho DW
│   │   └── beaufort.py                       # Hàm convert m/s → Beaufort number
│   │
│   ├── data/
│   │   └── danang_storm_oct2023.json         # Historical data bão Đà Nẵng 10/2023 (dùng cho demo)
│   │
│   ├── requirements.txt                      # pip dependencies
│   └── prefect.yaml                          # Prefect deployment config (schedule, work pool)
│
├── infra/                                    # Infrastructure as config
│   │
│   ├── docker-compose.yml                    # Dev local: tất cả services
│   │   # Services: postgres, prefect-server, prefect-worker, metabase
│   │   # FE và BE chạy native khi dev, chỉ docker hóa khi prod
│   │
│   ├── docker-compose.prod.yml               # Production: thêm nginx, dùng image từ registry
│   │
│   ├── nginx/
│   │   └── nginx.conf                        # Reverse proxy: / → FE, /api → BE, /metabase → Metabase
│   │
│   └── metabase/
│       └── metabase.env.example              # Biến môi trường Metabase (không commit file thật)
│
├── scripts/                                  # SQL scripts tiện ích
│   ├── seed_data.sql                         # Seed cảng mẫu, zones, users cho dev
│   ├── seed_sop_rules.sql                    # 10 SOP rules mẫu (risk × zone_type)
│   └── demo_simulation_data.sql              # Insert historical weather data cho Simulation Mode
│
├── .env.example                              # Template biến môi trường — commit file này
├── .gitignore                                # Node modules, build outputs, .env thật, *.user
├── README.md                                 # Hướng dẫn cài đặt và chạy dự án
└── CONTRIBUTING.md                           # File này — cấu trúc + Git workflow
```

---

## 2. Quy tắc Quản lý Nhánh (Branch Strategy)

### Mô hình nhánh

```
main          ← Production-ready. Chỉ PM merge vào đây. Không bao giờ push trực tiếp.
│
└── dev       ← Integration branch. Tất cả feature merge vào đây. PM review và merge.
    │
    ├── feature/be-b-auth          ← BE-B đang làm Auth
    ├── feature/be-b-port-zone     ← BE-B đang làm Port/Zone CRUD
    ├── feature/be-c-weather       ← BE-C đang làm Weather collector
    ├── feature/be-c-risk-engine   ← BE-C đang làm Risk Engine
    ├── feature/be-d-sop-engine    ← BE-D đang làm SOP Engine
    ├── feature/fe-a-dashboard     ← FE-A đang làm Dashboard
    ├── fix/be-c-beaufort-calc     ← Fix bug cụ thể
    └── fix/fe-a-alert-polling     ← Fix bug cụ thể
```

### Quy tắc đặt tên nhánh

```
<type>/<owner>-<mô-tả-ngắn-bằng-kebab-case>
```

| Type | Khi nào dùng | Ví dụ |
|------|-------------|-------|
| `feature/` | Tính năng mới theo feature list (F1–F14) | `feature/be-b-auth` |
| `fix/` | Sửa bug đã phát sinh | `fix/fe-a-dashboard-chart` |
| `chore/` | Việc kỹ thuật không phải tính năng (config, CI, deps) | `chore/pm-docker-compose` |
| `docs/` | Cập nhật tài liệu, API contract | `docs/be-c-swagger-risk` |

**Owner prefix:**

| Prefix | Thành viên |
|--------|-----------|
| `pm` | Nguyễn Phan Anh Minh (PM/DE) |
| `fe-a` | Trần Quang Dũng (FE) |
| `be-b` |  Nguyễn Anh Kiệt (BE-B) |
| `be-c` | Đinh Hải Quân (BE-C) |
| `be-d` | Võ Văn Kiên (BE-D) |

> ❌ **Không dùng:** `feature/fix-bug`, `test123`, `minh-branch`, `new-feature`  
> ✅ **Đúng:** `feature/be-c-risk-engine`, `fix/fe-a-alert-polling`

---

## 3. Commit Message Format

Nhóm dùng **Conventional Commits** — format chuẩn, dễ đọc lịch sử, CI có thể parse.

```
<type>(<scope>): <mô tả ngắn, tiếng Anh hoặc Việt, không viết hoa chữ đầu>

[body tùy chọn — giải thích WHY, không phải WHAT]

[footer tùy chọn — Closes #issue, Breaking change]
```

### Type hợp lệ

| Type | Ý nghĩa |
|------|---------|
| `feat` | Tính năng mới |
| `fix` | Sửa bug |
| `refactor` | Tái cấu trúc code, không thay đổi behavior |
| `test` | Thêm hoặc sửa test |
| `docs` | Cập nhật tài liệu, comment, README |
| `chore` | Config, dependencies, CI/CD |
| `style` | Format code, không đổi logic |

### Scope (bắt buộc)

Dùng tên module: `auth`, `port`, `zone`, `weather`, `risk`, `sop`, `mode`, `alert`, `task`, `etl`, `dashboard`, `infra`

### Ví dụ commit đúng

```
feat(auth): add JWT refresh token endpoint

Thêm POST /auth/refresh nhận refresh_token, trả access_token mới.
Refresh token lưu trong HttpOnly cookie, TTL 7 ngày.

Closes #12
```

```
feat(risk): implement Beaufort + rainfall worst-case aggregation
```

```
fix(dashboard): alert bell không cập nhật sau khi mark-read
```

```
chore(infra): add metabase service vào docker-compose
```

```
test(risk): add unit test cho ngưỡng CRITICAL (Beaufort >= 10)
```

### Ví dụ commit sai

```
❌ fix bug
❌ Update code
❌ WIP
❌ asdfgh
❌ Sửa lỗi dashboard không hiển thị được chart
```

> **Tip:** Nếu bạn không biết viết gì ngoài "fix bug" thì message đó chưa đủ thông tin. Hãy trả lời: *fix bug gì? ở đâu? tại sao xảy ra?*

---

## 4. Pull Request Process

### Khi nào tạo PR?

Tạo PR khi feature/fix đã:
- [ ] Code chạy được, không có compile error
- [ ] Đã test thủ công các case chính (happy path + 1 edge case)
- [ ] Commit message đúng format
- [ ] Không commit file `.env`, `appsettings.Development.json`, `node_modules/`

### Quy trình PR vào `dev`

```
1. Push nhánh feature lên GitHub
2. Tạo PR: feature/xxx → dev
3. Điền PR Template (mô tả thay đổi, cách test, screenshot nếu có FE)
4. Gắn Reviewer: bắt buộc ít nhất 1 người — PM hoặc 1 BE khác
5. Reviewer để lại comment nếu cần sửa, hoặc Approve
6. Sau khi có ít nhất 1 Approve: PM (Minh) merge vào dev bằng "Squash and Merge"
7. Xóa nhánh feature sau khi merge (GitHub hỗ trợ auto-delete)
```

### Quy trình PR vào `main`

```
1. Chỉ xảy ra tại các milestone: T3, T5, T7, T9, T10
2. PM tạo PR: dev → main
3. Toàn nhóm review nhanh (hoặc PM review kỹ)
4. PM merge vào main bằng "Merge Commit" (giữ nguyên lịch sử)
5. PM tạo GitHub Release tag: v0.1-t3, v0.2-t5, v1.0-final
```

### Ai có quyền merge?

| Merge vào | Người thực hiện | Điều kiện |
|-----------|----------------|-----------|
| `dev` | **PM (Minh)** | Có ≥ 1 Approve, CI pass |
| `main` | **PM (Minh)** | Milestone đạt, demo ổn định |

> **Lý do:** Tập trung quyền merge vào 1 người tránh conflict lộn xộn, đặc biệt khi 5 người làm song song. PM có cái nhìn tổng thể về trạng thái các module.

### Reviewer Guidelines

Khi review PR của người khác, tập trung vào:
- **Correctness:** Logic có đúng không? Edge case có được xử lý không?
- **API contract:** Response format có đúng với Swagger đã chốt không?
- **Security:** Có quên check RBAC không? Có lộ thông tin nhạy cảm trong log không?
- **Naming:** Tên biến/hàm có rõ nghĩa không?

Không cần review style (Prettier/ESLint/dotnet-format lo việc đó).

---

## 5. Quy tắc bổ sung

### Không được phép

- ❌ Push thẳng lên `dev` hoặc `main` (dù là PM)
- ❌ Merge PR của chính mình mà không có review từ người khác
- ❌ Commit file chứa secret: API key, password, connection string thật
- ❌ Force push lên `dev` hoặc `main`
- ❌ Bắt đầu code business logic trước khi API contract và DB schema được merge (T1)

### Nên làm

- ✅ Push early, push often — nhánh feature nên push lên GitHub hàng ngày dù chưa xong
- ✅ Tạo Draft PR khi đang làm, chuyển thành Ready khi hoàn thành — team biết bạn đang làm gì
- ✅ Tag tên người liên quan trong comment PR khi cần unblock: `@be-c xem giúp mình endpoint này`
- ✅ Mỗi khi merge xong một API mới vào dev, notify FE-A trong nhóm chat để FE-A bỏ mock data

### Sync code thường xuyên

Mỗi sáng trước khi code, pull dev mới nhất về nhánh của mình:

```bash
git checkout feature/be-c-risk-engine
git fetch origin
git rebase origin/dev
# Giải quyết conflict nếu có, sau đó tiếp tục
```

Dùng **rebase** thay vì merge để giữ lịch sử nhánh sạch.

---

## 6. Cài đặt môi trường Local (Quick Start)

```bash
# 1. Clone repo
git clone git@github.com:kat1448/CapstoneSu26_PORMS.git
cd porms

# 2. Copy và điền biến môi trường
cp .env.example .env
# Điền: OPENWEATHER_API_KEY, POSTGRES_PASSWORD, JWT_SECRET

# 3. Khởi động services nền (PostgreSQL, Prefect, Metabase)
docker compose up -d

# 4. Chạy DB migration
cd backend
dotnet ef database update --project PORMS.Infrastructure --startup-project PORMS.API

# 5. Seed dữ liệu mẫu
psql -h localhost -U postgres -d porms -f ../scripts/seed_data.sql
psql -h localhost -U postgres -d porms -f ../scripts/seed_sop_rules.sql

# 6. Chạy backend
cd PORMS.API
dotnet run

# 7. Chạy frontend (terminal khác)
cd ../../frontend
npm install
npm run dev

# 8. Chạy ETL (terminal khác, nếu cần)
cd ../etl
pip install -r requirements.txt
prefect server start   # Nếu không dùng Docker Prefect
python flows/weather_collector.py
```

**Ports mặc định:**
| Service | URL |
|---------|-----|
| Frontend (Vite) | http://localhost:5173 |
| Backend API | http://localhost:5000 |
| Swagger UI | http://localhost:5000/swagger |
| PostgreSQL | localhost:5432 |
| Prefect UI | http://localhost:4200 |
| Metabase | http://localhost:3000 |

---
