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

export const operationEventLabels: Record<string, string> = {
  WEATHER_FETCHED: "Đã cập nhật dữ liệu thời tiết",
  RISK_ASSESSED: "Đã đánh giá mức độ rủi ro",
  RISK_EVALUATED: "Đã hoàn tất đánh giá rủi ro",
  RISK_CHANGED: "Mức rủi ro thay đổi",
  RISK_LEVEL_CHANGED: "Mức rủi ro thay đổi",
  SOP_TRIGGERED: "Đã kích hoạt quy trình ứng phó",
  SOP_EXECUTED: "Đã thực hiện quy trình ứng phó",
  MODE_CHANGED: "Đã thay đổi chế độ vận hành",
  MODE_OVERRIDDEN: "Đã điều chỉnh chế độ vận hành",
  TASK_CREATED: "Đã tạo nhiệm vụ ứng phó",
  TASK_ASSIGNED: "Đã phân công nhiệm vụ",
  TASK_UNASSIGNED: "Đã thu hồi phân công",
  TASK_ACKNOWLEDGED: "Đã tiếp nhận nhiệm vụ",
  TASK_STARTED: "Đã bắt đầu nhiệm vụ",
  TASK_COMPLETED: "Đã hoàn tất nhiệm vụ",
  ALERT_CREATED: "Đã phát cảnh báo mới",
  ALERT_READ: "Đã xem cảnh báo",
  ALERT_ACKNOWLEDGED: "Đã xác nhận cảnh báo",
  THRESHOLD_UPDATED: "Đã cập nhật mức cảnh báo",
  SOP_RULE_UPDATED: "Đã cập nhật quy trình ứng phó",
  SIMULATION_STARTED: "Bắt đầu mô phỏng",
  SIMULATION_STEP: "Cập nhật tình huống mô phỏng",
  SIMULATION_COMPLETED: "Hoàn tất mô phỏng",
  SIMULATION_ENDED: "Kết thúc mô phỏng",
  USER_LOGIN: "Đăng nhập hệ thống",
  USER_LOGOUT: "Đăng xuất hệ thống",
  SYSTEM_TEST: "Kiểm tra hệ thống",
  REPORT_EXPORTED: "Đã xuất báo cáo"
};

export function operationEventLabel(value: string | null | undefined) {
  if (!value) return "Sự kiện vận hành";
  return operationEventLabels[value.toUpperCase()] ?? "Cập nhật vận hành";
}

export function actorDisplayLabel(value: string | null | undefined) {
  if (!value || value.toUpperCase() === "SYSTEM") return "Hệ thống PORMS";
  if (value.toUpperCase() === "PORMS DEMO OPERATOR") return "Nhân viên vận hành";
  return value;
}

export function operationEventSummaryLabel(value: string | null | undefined) {
  if (!value) return "Hệ thống vừa ghi nhận một cập nhật vận hành.";
  return value
    .replace(/\bWEATHER_FETCHED\b/gi, "đã cập nhật dữ liệu thời tiết")
    .replace(/\bRISK_(?:ASSESSED|EVALUATED)\b/gi, "đã đánh giá mức độ rủi ro")
    .replace(/\bRISK_LEVEL_CHANGED\b/gi, "mức rủi ro thay đổi")
    .replace(/\bSOP_TRIGGERED\b/gi, "đã kích hoạt quy trình ứng phó")
    .replace(/\bTASK_CREATED\b/gi, "đã tạo nhiệm vụ ứng phó")
    .replace(/\bTASK_ASSIGNED\b/gi, "đã phân công nhiệm vụ")
    .replace(/\bTASK_ACKNOWLEDGED\b/gi, "đã tiếp nhận nhiệm vụ")
    .replace(/\bTASK_STARTED\b/gi, "đã bắt đầu nhiệm vụ")
    .replace(/\bTASK_COMPLETED\b/gi, "đã hoàn tất nhiệm vụ")
    .replace(/\bALERT_CREATED\b/gi, "đã phát cảnh báo mới")
    .replace(/\bMODE_CHANGED\b/gi, "đã thay đổi chế độ vận hành")
    .replace(/\bSIMULATION_STARTED\b/gi, "bắt đầu mô phỏng")
    .replace(/\bSIMULATION_STEP\b/gi, "cập nhật tình huống mô phỏng")
    .replace(/\bSIMULATION_(?:COMPLETED|ENDED)\b/gi, "hoàn tất mô phỏng")
    .replace(/\bLOW\b/gi, "Thấp")
    .replace(/\bMEDIUM\b/gi, "Cần lưu ý")
    .replace(/\bHIGH\b/gi, "Cao")
    .replace(/\bCRITICAL\b/gi, "Rất cao")
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

/** Chuyển phần giải thích tự động thành câu chữ thân thiện với người vận hành. */
export function forecastTextLabel(value: string | null | undefined) {
  if (!value) return "";
  const replacements: Array<[RegExp, string]> = [
    [/PCA\s*risk\s*score\s*(\d+)/gi, "Điểm rủi ro dự báo $1"],
    [/rule\s*risk\s*level\s*(LOW|MEDIUM|HIGH|CRITICAL)/gi, "mức rủi ro theo quy tắc: $1"],
    [/PCA\/K-?Means/gi, "phân tích xu hướng"],
    [/\bPCA\b|\bK-?Means\b|\bLLM\b/gi, "phân tích tự động"],
    [/\bNORMAL\b/gi, "bình thường"],
    [/\bLIMITED\b/gi, "hạn chế"],
    [/\bSTOP\b/gi, "tạm dừng"],
    [/\bCRITICAL\b/gi, "rất cao"],
    [/\bHIGH\b/gi, "cao"],
    [/\bMEDIUM\b/gi, "cần lưu ý"],
    [/\bLOW\b/gi, "thấp"],
    [/Khong thay doi/gi, "Giữ nguyên phương án vận hành"],
    [/Chuyen sang che do van hanh gioi han/gi, "Chuyển sang vận hành hạn chế"],
    [/Thoi tiet stable/gi, "Thời tiết ổn định"],
    [/Giam toc do thao tac xep do container/gi, "Giảm tốc độ xếp dỡ container"],
    [/Bat den chieu sang va phat tin hieu canh bao tam nhin giam/gi, "Bật đèn chiếu sáng và phát tín hiệu khi tầm nhìn giảm"],
    [/Tang cuong giam sat an toan hoa tieu va cap tau/gi, "Tăng cường giám sát an toàn hoa tiêu và cập tàu"],
    [/Duy tri ke hoach van hanh tieu chuan/gi, "Duy trì kế hoạch vận hành tiêu chuẩn"],
    [/Theo doi toc do gio va tam nhin/gi, "Theo dõi tốc độ gió và tầm nhìn"],
    [/Cap nhat du bao thoi tiet cho ngay tiep theo/gi, "Cập nhật dự báo thời tiết cho ngày tiếp theo"],
    [/Kiem tra dinh ky an toan thiet bi be cau/gi, "Kiểm tra định kỳ an toàn thiết bị bốc dỡ"],
    [/Toan bo hoat dong van hanh ca tieng va cau tau/gi, "Toàn bộ hoạt động vận hành cảng và cầu tàu"],
    [/Hoat dong xep do container tren bai va cau tau/gi, "Hoạt động xếp dỡ container trên bãi và cầu tàu"],
    [/Hoat dong hoa tieu va di chuyen tau trong luong/gi, "Hoạt động hoa tiêu và di chuyển tàu trong luồng"]
  ];
  return replacements.reduce((text, [pattern, replacement]) => text.replace(pattern, replacement), value)
    .replace(/\s{2,}/g, " ").trim();
}

export function clusterLabel(value: string | null | undefined) {
  const labels: Record<string, string> = {
    STABLE_WEATHER: "Thời tiết ổn định",
    WIND_RISK: "Gió mạnh cần lưu ý",
    RAIN_RISK: "Mưa lớn cần lưu ý",
    RAIN_VISIBILITY_RISK: "Mưa kèm tầm nhìn kém",
    LOW_VISIBILITY: "Tầm nhìn hạn chế",
    SEVERE_WEATHER: "Thời tiết nguy hiểm"
  };
  if (!value) return "Chưa phân nhóm";
  return labels[value.toUpperCase()] ?? "Xu hướng thời tiết khác";
}

export function dataSourceLabel(value: string | null | undefined) {
  if (!value) return "Chưa xác định";
  const source = value.toUpperCase();
  if (source.includes("DEMO_BACKFILL")) return "Dữ liệu minh họa bổ sung";
  if (source.includes("OPENWEATHER")) return "Dữ liệu thời tiết trực tuyến";
  if (source.includes("SIMULATION")) return "Dữ liệu mô phỏng";
  if (source.includes("MANUAL")) return "Dữ liệu nhập thủ công";
  return "Nguồn dữ liệu hệ thống";
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
    .replace(/^Demo simulation started for port (.+)\.$/i, "Đã bắt đầu kịch bản thời tiết mẫu tại cảng $1.")
    .replace(/^Demo simulation completed for port (.+)\.$/i, "Kịch bản thời tiết mẫu tại cảng $1 đã hoàn tất.")
    .replace(/^Simulation step (\d+) advanced port (.+) to (LOW|MEDIUM|HIGH|CRITICAL)\.$/i, (_match, step, area, risk) =>
      `Bước ${step}: Cảng ${area} chuyển sang mức ${riskLabel(risk)}.`)
    .replace(/^Simulation dataset (.+) started\.$/i, "Đã bắt đầu chạy kịch bản “$1”.")
    .replace(/^Simulation dataset (.+) completed\.$/i, "Kịch bản “$1” đã hoàn tất.")
    .replace(/^Simulation step (\d+) moved (.+) to (LOW|MEDIUM|HIGH|CRITICAL)\.$/i, (_match, step, area, risk) =>
      `Bước ${step}: ${area} chuyển sang mức ${riskLabel(risk)}.`);
}
