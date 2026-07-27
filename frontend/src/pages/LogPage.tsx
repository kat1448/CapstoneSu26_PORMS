import { useCallback, useEffect, useMemo, useState } from "react";
import { Badge } from "../components/common/Badge";
import { useDemoRefresh } from "../hooks/useDemoRefresh";
import { getOperationEvents } from "../services/logService";
import type { OperationEvent } from "../types/log";
import { actorDisplayLabel, operationEventLabel, operationEventSummaryLabel, simulationDetailLabel } from "../utils/displayLabels";

type LogPageProps = {
  refreshKey: number;
};

type LogScope = "live" | "simulation";

type OperationLogRun = {
  events: OperationEvent[];
  firstAt: string;
  groupKey: string;
  lastAt: string;
  portCode: string;
  portName: string;
  riskTone: OperationEvent["tone"];
  runLabel: string;
  scenarioName?: string | null;
};

const tonePriority: Record<OperationEvent["tone"], number> = {
  danger: 4,
  warning: 3,
  success: 2,
  info: 1
};

const PAGE_SIZE = 15;
const toneOptions: OperationEvent["tone"][] = ["info", "success", "warning", "danger"];
const toneLabels: Record<OperationEvent["tone"], string> = {
  danger: "Nguy hiểm",
  info: "Thông tin",
  success: "Hoàn tất",
  warning: "Cần chú ý"
};

function uniqueBy<T>(items: T[], keyOf: (item: T) => string) {
  const seen = new Set<string>();
  return items.filter((item) => {
    const key = keyOf(item);
    if (seen.has(key)) return false;
    seen.add(key);
    return true;
  });
}

function badgeTone(tone: OperationEvent["tone"]): "danger" | "info" | "success" | "warning" {
  if (tone === "danger") return "danger";
  if (tone === "warning") return "warning";
  if (tone === "success") return "success";
  return "info";
}

function eventDateValue(event: OperationEvent) {
  return event.occurredAtRaw.slice(0, 10);
}

function zoneLabel(event: OperationEvent) {
  return event.zoneName ?? "Toàn cảng";
}

function includesSearch(value: string | null | undefined, searchTerm: string) {
  return (value ?? "").toLocaleLowerCase("vi-VN").includes(searchTerm);
}

function getRunBucket(event: OperationEvent) {
  if (event.simulationSessionId) {
    return event.simulationSessionId;
  }

  const date = new Date(event.occurredAtRaw);
  if (Number.isNaN(date.getTime())) {
    return event.occurredAt;
  }

  const bucket = new Date(date);
  bucket.setMinutes(0, 0, 0);
  return bucket.toISOString();
}

function groupOperationRuns(events: OperationEvent[]): OperationLogRun[] {
  const groups = new Map<string, OperationEvent[]>();

  events.forEach((event) => {
    const key = `${event.portId ?? event.portCode}|${getRunBucket(event)}`;
    groups.set(key, [...(groups.get(key) ?? []), event]);
  });

  return Array.from(groups.entries()).map(([groupKey, groupEvents]) => {
    const sortedEvents = [...groupEvents].sort((a, b) => new Date(a.occurredAtRaw).getTime() - new Date(b.occurredAtRaw).getTime());
    const newest = sortedEvents[sortedEvents.length - 1];
    const oldest = sortedEvents[0];
    const strongestTone = sortedEvents.reduce<OperationEvent["tone"]>((current, event) => (
      tonePriority[event.tone] > tonePriority[current] ? event.tone : current
    ), "info");

    return {
      events: sortedEvents,
      firstAt: oldest?.occurredAt ?? "-",
      groupKey,
      lastAt: newest?.occurredAt ?? "-",
      portCode: newest?.portCode ?? "N/A",
      portName: newest?.portName ?? newest?.portCode ?? "Không xác định",
      riskTone: strongestTone,
      runLabel: newest?.simulationDatasetName ?? (newest?.simulationSessionId ? "Kịch bản mô phỏng" : `Lần vận hành ${newest?.portName ?? ""}`),
      scenarioName: newest?.simulationDatasetName ?? null
    };
  }).sort((a, b) => new Date(b.events[b.events.length - 1]?.occurredAtRaw ?? 0).getTime() - new Date(a.events[a.events.length - 1]?.occurredAtRaw ?? 0).getTime());
}

export function LogPage({ refreshKey }: LogPageProps) {
  useDemoRefresh();
  const [events, setEvents] = useState<OperationEvent[]>([]);
  const [scope, setScope] = useState<LogScope>("live");
  const [currentPage, setCurrentPage] = useState(1);
  const [selectedRunKey, setSelectedRunKey] = useState<string | null>(null);
  const [selectedPortId, setSelectedPortId] = useState("");
  const [selectedZoneName, setSelectedZoneName] = useState("");
  const [selectedTone, setSelectedTone] = useState("");
  const [simulationSearchTerm, setSimulationSearchTerm] = useState("");
  const [fromDate, setFromDate] = useState("");
  const [toDate, setToDate] = useState("");

  const loadEvents = useCallback(() => {
    void getOperationEvents(scope).then(setEvents);
  }, [scope]);

  useEffect(() => {
    setCurrentPage(1);
    setSelectedRunKey(null);
    setSimulationSearchTerm("");
    loadEvents();
  }, [loadEvents, refreshKey]);

  useEffect(() => {
    const timer = window.setInterval(loadEvents, 600_000);
    return () => window.clearInterval(timer);
  }, [loadEvents]);

  const runs = useMemo(() => groupOperationRuns(events), [events]);
  const portOptions = useMemo(
    () => uniqueBy(events, (event) => event.portId ?? event.portCode).map((event) => ({
      label: `${event.portCode} - ${event.portName}`,
      portId: event.portId ?? event.portCode
    })),
    [events]
  );
  const zoneOptions = useMemo(() => {
    const scopedEvents = selectedPortId
      ? events.filter((event) => (event.portId ?? event.portCode) === selectedPortId)
      : events;
    return [...new Set(scopedEvents.map(zoneLabel).filter(Boolean))].sort();
  }, [events, selectedPortId]);

  useEffect(() => {
    if (selectedZoneName && !zoneOptions.includes(selectedZoneName)) {
      setSelectedZoneName("");
    }
  }, [selectedZoneName, zoneOptions]);

  const filteredRuns = useMemo(() => runs.filter((run) => {
    const runPortId = run.events[0]?.portId ?? run.portCode;
    const matchesZone = !selectedZoneName || run.events.some((event) => zoneLabel(event) === selectedZoneName);
    const matchesDateRange = run.events.some((event) => {
      const eventDate = eventDateValue(event);
      return (!fromDate || eventDate >= fromDate) && (!toDate || eventDate <= toDate);
    });
    const searchTerm = simulationSearchTerm.trim().toLocaleLowerCase("vi-VN");
    const matchesSimulationSearch = !searchTerm
      || includesSearch(run.scenarioName, searchTerm)
      || includesSearch(run.runLabel, searchTerm)
      || run.events.some((event) => (
        includesSearch(event.simulationSessionId, searchTerm)
        || includesSearch(event.simulationDatasetName, searchTerm)
        || includesSearch(event.summary, searchTerm)
      ));

    return (!selectedPortId || runPortId === selectedPortId)
      && matchesZone
      && (!selectedTone || run.riskTone === selectedTone)
      && matchesDateRange
      && (scope !== "simulation" || matchesSimulationSearch);
  }), [fromDate, runs, scope, selectedPortId, selectedTone, selectedZoneName, simulationSearchTerm, toDate]);

  const totalPages = Math.max(1, Math.ceil(filteredRuns.length / PAGE_SIZE));
  const visibleRuns = useMemo(() => {
    const start = (currentPage - 1) * PAGE_SIZE;
    return filteredRuns.slice(start, start + PAGE_SIZE);
  }, [currentPage, filteredRuns]);
  const selectedRun = runs.find((run) => run.groupKey === selectedRunKey) ?? null;

  useEffect(() => {
    setCurrentPage((page) => Math.min(page, totalPages));
  }, [totalPages]);

  useEffect(() => {
    setCurrentPage(1);
    setSelectedRunKey(null);
  }, [fromDate, selectedPortId, selectedTone, selectedZoneName, simulationSearchTerm, toDate]);

  function resetFilters() {
    setSelectedPortId("");
    setSelectedZoneName("");
    setSelectedTone("");
    setSimulationSearchTerm("");
    setFromDate("");
    setToDate("");
  }

  return (
    <section className="page-grid">
      <div className="section-heading">
        <div>
          <h2>Nhật ký vận hành</h2>
          <p>Theo dõi các sự kiện đã xảy ra trong từng ca vận hành và từng lần mô phỏng.</p>
        </div>
        <div aria-label="Loại nhật ký" className="segmented-control" role="tablist">
          <button
            aria-selected={scope === "live"}
            className={scope === "live" ? "active" : ""}
            onClick={() => setScope("live")}
            role="tab"
            type="button"
          >
            Nhật ký thật
          </button>
          <button
            aria-selected={scope === "simulation"}
            className={scope === "simulation" ? "active" : ""}
            onClick={() => setScope("simulation")}
            role="tab"
            type="button"
          >
            Nhật ký mô phỏng
          </button>
        </div>
      </div>

      {!selectedRun ? (
        <>
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
            <span>Cấp độ</span>
            <select className="select-input" onChange={(event) => setSelectedTone(event.target.value)} value={selectedTone}>
              <option value="">Tất cả cấp độ</option>
              {toneOptions.map((tone) => (
                <option key={tone} value={tone}>{toneLabels[tone]}</option>
              ))}
            </select>
          </label>
          {scope === "simulation" ? (
            <label>
              <span>Phiên/kịch bản</span>
              <input
                className="input"
                onChange={(event) => setSimulationSearchTerm(event.target.value)}
                placeholder="Nhập mã phiên hoặc tên kịch bản"
                type="search"
                value={simulationSearchTerm}
              />
            </label>
          ) : null}
          <button className="button button-secondary button-small" onClick={resetFilters} type="button">Xóa lọc</button>
        </div>
        <article className="card timeline-card">
          {filteredRuns.length === 0 ? (
            <div className="empty-state">
              <strong>{runs.length === 0 ? "Chưa có nhật ký" : "Không có nhật ký phù hợp"}</strong>
              <span>{runs.length === 0 ? (scope === "simulation" ? "Chạy mô phỏng để xem sự kiện riêng." : "Chưa có sự kiện vận hành thật.") : "Thử thay đổi bộ lọc để xem thêm nhật ký vận hành."}</span>
            </div>
          ) : null}
          {visibleRuns.map((run) => (
            <div className={`timeline-item tone-${run.riskTone}`} key={run.groupKey}>
              <div className="timeline-header">
                <strong>{run.portCode}</strong>
                <small>{run.firstAt} - {run.lastAt}</small>
              </div>
              <div className="timeline-meta">
                <Badge tone={badgeTone(run.riskTone)}>{run.events.length} nhật ký</Badge>
                <span>{run.portName}</span>
                <span>{run.runLabel}</span>
                {run.scenarioName && run.scenarioName !== run.runLabel ? <span>{run.scenarioName}</span> : null}
                <button className="button button-secondary button-small" onClick={() => setSelectedRunKey(run.groupKey)} type="button">
                  Chi tiết
                </button>
              </div>
            </div>
          ))}
          {totalPages > 1 ? (
            <div className="table-pagination" aria-label="Phân trang nhật ký vận hành">
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
        </>
      ) : (
        <section className="simulation-results-page operation-log-detail">
          <div className="section-heading">
            <div>
              <h2>Chi tiết nhật ký vận hành</h2>
              <p>{selectedRun.portName} · Từ {selectedRun.firstAt} đến {selectedRun.lastAt}</p>
            </div>
            <button className="button button-secondary" onClick={() => setSelectedRunKey(null)} type="button">Quay lại</button>
          </div>

          <div className="simulation-results-kpis">
            <article className="card card-pad simulation-results-kpi">
              <span>Cảng</span>
              <strong>{selectedRun.portName}</strong>
              <small>Khu vực được ghi nhận</small>
            </article>
            <article className="card card-pad simulation-results-kpi">
              <span>Số sự kiện</span>
              <strong>{selectedRun.events.length}</strong>
              <small>{selectedRun.runLabel}</small>
            </article>
            <article className="card card-pad simulation-results-kpi risk">
              <span>Mức cần chú ý</span>
              <strong>{toneLabels[selectedRun.riskTone]}</strong>
              <Badge tone={badgeTone(selectedRun.riskTone)}>{toneLabels[selectedRun.riskTone]}</Badge>
            </article>
          </div>

          <article className="card card-pad simulation-results-card">
            <div className="card-head">
              <div>
                <h3>Diễn biến hoạt động</h3>
                <p>Các sự kiện được sắp xếp theo thời gian để dễ theo dõi toàn bộ quá trình.</p>
              </div>
              <Badge tone="info">{selectedRun.events.length} dòng</Badge>
            </div>
            <div aria-label="Danh sách diễn biến vận hành" className="operation-event-list" role="list">
              {selectedRun.events.map((event, index) => (
                <article className={`operation-event-item tone-${event.tone}`} key={event.operationEventId} role="listitem">
                  <div className="operation-event-index" aria-hidden="true">{index + 1}</div>
                  <div className="operation-event-main">
                    <div className="operation-event-head">
                      <strong>{event.isSimulation ? simulationDetailLabel(operationEventSummaryLabel(event.summary)) : operationEventSummaryLabel(event.summary)}</strong>
                      <time>{event.occurredAt}</time>
                    </div>
                    <div className="operation-event-meta">
                      <Badge tone={badgeTone(event.tone)}>{operationEventLabel(event.eventType)}</Badge>
                      <span>Vị trí: {event.zoneName ?? "Toàn cảng"}</span>
                      <span>Ghi nhận bởi: {actorDisplayLabel(event.actorName)}</span>
                    </div>
                  </div>
                </article>
              ))}
            </div>
          </article>
        </section>
      )}
    </section>
  );
}
