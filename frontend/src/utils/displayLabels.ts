export const riskLabels: Record<string, string> = {
  LOW: "Thấp",
  MEDIUM: "Cần lưu ý",
  HIGH: "Cao",
  CRITICAL: "Rất cao"
};

export const operationModeLabels: Record<string, string> = {
  NORMAL: "Vận hành bình thường",
  LIMITED: "Hạn chế vận hành",
  STOP: "Tạm dừng vận hành"
};

export const simulationEventLabels: Record<string, string> = {
  SIMULATION_STARTED: "Bắt đầu mô phỏng",
  SIMULATION_STEP: "Cập nhật tình huống",
  SIMULATION_COMPLETED: "Đã hoàn tất mô phỏng"
};

/** Tên sự kiện hiển thị cho người vận hành (không lộ mã nội bộ của hệ thống). */
export const operationEventLabels: Record<string, string> = {
  WEATHER_FETCHED: "Đã cập nhật thời tiết",
  RISK_ASSESSED: "Đã đánh giá mức rủi ro",
  RISK_CHANGED: "Mức rủi ro thay đổi",
  RISK_LEVEL_CHANGED: "Mức rủi ro thay đổi",
  SOP_TRIGGERED: "Đã kích hoạt quy trình ứng phó",
  SOP_EXECUTED: "Đã thực hiện quy trình ứng phó",
  MODE_CHANGED: "Đã thay đổi chế độ vận hành",
  MODE_OVERRIDDEN: "Đã điều chỉnh chế độ vận hành",
  TASK_CREATED: "Đã tạo nhiệm vụ ứng phó",
  ALERT_CREATED: "Đã phát cảnh báo",
  ALERT_READ: "Đã xem cảnh báo",
  THRESHOLD_UPDATED: "Đã cập nhật ngưỡng rủi ro",
  SOP_RULE_UPDATED: "Đã cập nhật quy tắc ứng phó",
  SIMULATION_STARTED: "Bắt đầu mô phỏng",
  SIMULATION_STEP: "Cập nhật tình huống",
  SIMULATION_COMPLETED: "Hoàn tất mô phỏng",
  SIMULATION_ENDED: "Kết thúc mô phỏng",
  USER_LOGIN: "Đăng nhập hệ thống",
  USER_LOGOUT: "Đăng xuất hệ thống",
  SYSTEM_TEST: "Kiểm tra hệ thống"
};

export function operationEventLabel(value: string | null | undefined) {
  if (!value) return "Sự kiện hệ thống";
  return operationEventLabels[value.toUpperCase()] ?? value.replace(/_/g, " ").toLocaleLowerCase("vi-VN");
}

export function actorDisplayLabel(value: string | null | undefined) {
  if (!value || value.toUpperCase() === "SYSTEM") return "Hệ thống PORMS";
  if (value.toUpperCase() === "PORMS DEMO OPERATOR") return "Nhân viên vận hành";
  return value;
}

/** Làm mềm những mã mức độ còn sót trong câu mô tả do dữ liệu cũ tạo ra. */
export function operationEventSummaryLabel(value: string | null | undefined) {
  if (!value) return "Hệ thống vừa ghi nhận một sự kiện.";
  return value
    .replace(/\bWEATHER_FETCHED\b/gi, "đã cập nhật thời tiết")
    .replace(/\bRISK_ASSESSED\b/gi, "đã đánh giá mức rủi ro")
    .replace(/\bRISK_LEVEL_CHANGED\b/gi, "mức rủi ro thay đổi")
    .replace(/\bSOP_TRIGGERED\b/gi, "đã kích hoạt quy trình ứng phó")
    .replace(/\bTASK_CREATED\b/gi, "đã tạo nhiệm vụ ứng phó")
    .replace(/\bALERT_CREATED\b/gi, "đã phát cảnh báo")
    .replace(/\bMODE_CHANGED\b/gi, "đã thay đổi chế độ vận hành")
    .replace(/\b(LOW)\b/gi, "Thấp")
    .replace(/\b(MEDIUM)\b/gi, "Cần lưu ý")
    .replace(/\b(HIGH)\b/gi, "Cao")
    .replace(/\b(CRITICAL)\b/gi, "Rất cao")
    .replace(/\bNORMAL\b/gi, "bình thường")
    .replace(/\bLIMITED\b/gi, "hạn chế")
    .replace(/\bSTOP\b/gi, "tạm dừng");
}

export function riskLabel(value: string | null | undefined) {
  if (!value) return "Chưa xác định";
  return riskLabels[value.toUpperCase()] ?? value;
}

export function operationModeLabel(value: string | null | undefined) {
  if (!value) return "Chưa xác định";
  return operationModeLabels[value.toUpperCase()] ?? value;
}

export function clusterLabel(value: string | null | undefined) {
  const labels: Record<string, string> = {
    STABLE_WEATHER: "Thời tiết ổn định",
    WIND_RISK: "Gió mạnh cần lưu ý",
    RAIN_RISK: "Mưa lớn cần lưu ý",
    LOW_VISIBILITY: "Tầm nhìn hạn chế",
    SEVERE_WEATHER: "Thời tiết nguy hiểm"
  };
  if (!value) return "Chưa phân nhóm";
  return labels[value.toUpperCase()] ?? value.replace(/_/g, " ").toLocaleLowerCase("vi-VN");
}

export function dataSourceLabel(value: string | null | undefined) {
  if (!value) return "Chưa xác định";
  const source = value.toUpperCase();
  if (source.includes("DEMO_BACKFILL")) return "Dữ liệu bù mô phỏng";
  if (source.includes("OPENWEATHER")) return "Dữ liệu thời tiết trực tuyến";
  if (source.includes("SIMULATION")) return "Dữ liệu mô phỏng";
  if (source.includes("MANUAL")) return "Nhập thủ công";
  return value;
}

export function weatherDescriptionLabel(value: string | null | undefined) {
  if (!value?.trim()) return "Chưa có thông tin";
  const labels: Record<string, string> = {
    "clear sky": "Trời quang",
    "few clouds": "Ít mây",
    "scattered clouds": "Mây rải rác",
    "broken clouds": "Nhiều mây",
    "overcast clouds": "Trời âm u",
    "light rain": "Mưa nhẹ",
    "moderate rain": "Mưa vừa",
    "heavy intensity rain": "Mưa lớn",
    thunderstorm: "Dông",
    mist: "Sương mù nhẹ",
    fog: "Sương mù"
  };
  return labels[value.trim().toLocaleLowerCase("en-US")] ?? value;
}

export function simulationDetailLabel(value: string) {
  return value
    .replace(/^Demo simulation started for port (.+)\.$/i, 'Đã bắt đầu kịch bản bão mẫu tại cảng $1.')
    .replace(/^Demo simulation completed for port (.+)\.$/i, 'Kịch bản bão mẫu tại cảng $1 đã hoàn tất.')
    .replace(/^Simulation step (\d+) advanced port (.+) to (LOW|MEDIUM|HIGH|CRITICAL)\.$/i, (_match, step, area, risk) =>
      `Bước ${step}: Cảng ${area} chuyển sang mức ${riskLabel(risk)}.`)
    .replace(/^Simulation dataset (.+) started\.$/i, 'Đã bắt đầu chạy kịch bản “$1”.')
    .replace(/^Simulation dataset (.+) completed\.$/i, 'Kịch bản “$1” đã hoàn tất.')
    .replace(/^Simulation step (\d+) moved (.+) to (LOW|MEDIUM|HIGH|CRITICAL)\.$/i, (_match, step, area, risk) =>
      `Bước ${step}: ${area} chuyển sang mức ${riskLabel(risk)}.`);
}
