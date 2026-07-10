from __future__ import annotations

from datetime import datetime
from math import sqrt
from typing import Any


FEATURE_NAMES = [
    "wind_speed_ms",
    "rainfall_mm",
    "visibility_risk",
    "humidity_pct",
    "pressure_drop",
    "temperature_c",
    "rule_risk_score",
]

RISK_SCORES = {
    "LOW": 1.0,
    "MEDIUM": 2.0,
    "HIGH": 3.0,
    "CRITICAL": 4.0,
}


def analyze_forecast_risk(port_code: str, items: list[dict[str, Any]]) -> dict[str, Any]:
    if not items:
        raise ValueError("items must not be empty")

    vectors = [_build_feature_vector(item) for item in items]
    standardized = _standardize(vectors)
    pca_values = _first_principal_component(standardized)
    clusters = _k_means(standardized, min(3, len(items)))
    cluster_labels = _label_clusters(items, vectors, clusters)
    min_pca = min(pca_values)
    max_pca = max(pca_values)
    span = max(max_pca - min_pca, 0.000001)

    analysis_items = []
    for index, item in enumerate(items):
        cluster_id = clusters[index]
        pca_score = round(((pca_values[index] - min_pca) / span) * 100, 1)
        analysis_items.append(
            {
                "planned_at": _format_datetime(item.get("planned_at")),
                "rule_risk_level": str(item.get("rule_risk_level", "LOW")).upper(),
                "cluster_id": cluster_id,
                "cluster_label": cluster_labels[cluster_id],
                "pca_risk_score": pca_score,
                "ml_recommendation": _recommendation(cluster_labels[cluster_id], pca_score),
                "dominant_factors": _dominant_factors(vectors[index]),
            }
        )

    return {
        "model_version": "pca-kmeans-v1",
        "port_code": port_code,
        "items": analysis_items,
    }


def _build_feature_vector(item: dict[str, Any]) -> list[float]:
    visibility_km = _number(item.get("visibility_km"), 10.0)
    pressure_hpa = _number(item.get("pressure_hpa"), 1013.0)
    rule_level = str(item.get("rule_risk_level", "LOW")).upper()

    return [
        _number(item.get("wind_speed_ms"), 0.0),
        _number(item.get("rainfall_mm"), 0.0),
        max(0.0, 10.0 - visibility_km),
        _number(item.get("humidity_pct"), 0.0),
        max(0.0, 1013.0 - pressure_hpa),
        _number(item.get("temperature_c"), 0.0),
        RISK_SCORES.get(rule_level, 1.0),
    ]


def _standardize(vectors: list[list[float]]) -> list[list[float]]:
    columns = list(zip(*vectors))
    means = [sum(column) / len(column) for column in columns]
    stds = []
    for column, mean in zip(columns, means):
        variance = sum((value - mean) ** 2 for value in column) / len(column)
        stds.append(sqrt(variance) or 1.0)

    return [
        [(value - means[index]) / stds[index] for index, value in enumerate(vector)]
        for vector in vectors
    ]


def _first_principal_component(vectors: list[list[float]]) -> list[float]:
    width = len(vectors[0])
    component = [1.0 / sqrt(width)] * width

    for _ in range(25):
        projected = [_dot(vector, component) for vector in vectors]
        next_component = [0.0] * width
        for vector, projection in zip(vectors, projected):
            for index, value in enumerate(vector):
                next_component[index] += projection * value
        length = sqrt(sum(value * value for value in next_component)) or 1.0
        component = [value / length for value in next_component]

    scores = [_dot(vector, component) for vector in vectors]
    if scores.index(max(scores)) < scores.index(min(scores)):
        scores = [-score for score in scores]
    return scores


def _k_means(vectors: list[list[float]], k: int) -> list[int]:
    if k == 1:
        return [0 for _ in vectors]

    centroids = [vectors[index][:] for index in _initial_centroid_indexes(vectors, k)]
    assignments = [0 for _ in vectors]

    for _ in range(30):
        next_assignments = [
            min(range(k), key=lambda centroid_index: _distance(vector, centroids[centroid_index]))
            for vector in vectors
        ]
        if next_assignments == assignments:
            break
        assignments = next_assignments
        for centroid_index in range(k):
            members = [vector for vector, assigned in zip(vectors, assignments) if assigned == centroid_index]
            if members:
                centroids[centroid_index] = [
                    sum(member[feature_index] for member in members) / len(members)
                    for feature_index in range(len(vectors[0]))
                ]

    return assignments


def _initial_centroid_indexes(vectors: list[list[float]], k: int) -> list[int]:
    composite_scores = [vector[0] + vector[1] + vector[2] + vector[6] for vector in vectors]
    ordered = sorted(range(len(vectors)), key=lambda index: composite_scores[index])
    if k == 2:
        return [ordered[0], ordered[-1]]
    return [ordered[0], ordered[len(ordered) // 2], ordered[-1]]


def _label_clusters(items: list[dict[str, Any]], vectors: list[list[float]], clusters: list[int]) -> dict[int, str]:
    labels: dict[int, str] = {}
    for cluster_id in sorted(set(clusters)):
        indexes = [index for index, assigned in enumerate(clusters) if assigned == cluster_id]
        averages = [
            sum(vectors[index][feature_index] for index in indexes) / len(indexes)
            for feature_index in range(len(FEATURE_NAMES))
        ]
        max_rule = max(str(items[index].get("rule_risk_level", "LOW")).upper() for index in indexes)

        if max_rule == "CRITICAL" or averages[6] >= 3.5 or (averages[0] >= 20 and averages[1] >= 20):
            labels[cluster_id] = "SEVERE_OPERATION_RISK"
        elif averages[0] >= 12:
            labels[cluster_id] = "WIND_RISK"
        elif averages[1] >= 10 or averages[2] >= 5:
            labels[cluster_id] = "RAIN_VISIBILITY_RISK"
        else:
            labels[cluster_id] = "STABLE_WEATHER"
    return labels


def _recommendation(cluster_label: str, pca_score: float) -> str:
    if cluster_label == "SEVERE_OPERATION_RISK" or pca_score >= 75:
        return "STOP"
    if pca_score >= 50:
        return "LIMITED"
    return "NORMAL"


def _dominant_factors(vector: list[float]) -> list[str]:
    ranked = sorted(
        [
            ("WIND", vector[0] / 20.0),
            ("RAIN", vector[1] / 30.0),
            ("VISIBILITY", vector[2] / 8.0),
            ("HUMIDITY", vector[3] / 100.0),
            ("PRESSURE", vector[4] / 20.0),
            ("RULE", vector[6] / 4.0),
        ],
        key=lambda item: item[1],
        reverse=True,
    )
    return [name for name, score in ranked[:3] if score > 0]


def _number(value: Any, fallback: float) -> float:
    try:
        if value is None:
            return fallback
        return float(value)
    except (TypeError, ValueError):
        return fallback


def _format_datetime(value: Any) -> str:
    if isinstance(value, datetime):
        return value.isoformat()
    return str(value)


def _dot(left: list[float], right: list[float]) -> float:
    return sum(left_value * right_value for left_value, right_value in zip(left, right))


def _distance(left: list[float], right: list[float]) -> float:
    return sqrt(sum((left_value - right_value) ** 2 for left_value, right_value in zip(left, right)))
