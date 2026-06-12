using PORMS.Domain.Enums;

namespace PORMS.Application.DTOs.Mode;

public sealed record OverrideModeRequest(
    OperationMode TargetMode,
    string OverrideReason,
    Guid? UserId);
