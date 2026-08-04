namespace PORMS.API.Configuration
{
    /// Cấu hình xác thực dành riêng cho API nội bộ giữa Prefect và backend
    public sealed class InternalApiOptions
    {
        public const string SectionName = "InternalApi";

        public string Key { get; init; } = string.Empty;
    }
}
