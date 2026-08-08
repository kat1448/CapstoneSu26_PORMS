using ClosedXML.Excel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using NpgsqlTypes;
using PORMS.API.Configuration;
using PORMS.API.Contracts;
using PORMS.API.Services;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Xunit;

namespace PORMS.Tests.Integration
{
    /// Kiểm tra phân quyền và luồng tải - preview template Excel SOP
    [Collection(DatabaseBackedIntegrationCollection.Name)]
    public sealed class SopRuleImportAuthorizationTests
    {
        private readonly IntegrationTestWebApplicationFactory _factory;

        public SopRuleImportAuthorizationTests(
            IntegrationTestWebApplicationFactory factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task UnauthenticatedUser_CannotPreviewImport()
        {
            using var client = _factory.CreateClient();
            using var content = CreateMultipartContent(
                [1, 2, 3],
                "sop-rules.xlsx");

            var response = await client.PostAsync(
                "/api/sop-rules/import/preview",
                content);

            Assert.Equal(
                HttpStatusCode.Unauthorized,
                response.StatusCode);
        }

        [Fact]
        public async Task PortManager_CannotPreviewImport()
        {
            using var client =
                CreateAuthenticatedClient("PORT_MANAGER");

            using var content = CreateMultipartContent(
                [1, 2, 3],
                "sop-rules.xlsx");

            var response = await client.PostAsync(
                "/api/sop-rules/import/preview",
                content);

            Assert.Equal(
                HttpStatusCode.Forbidden,
                response.StatusCode);
        }

        [Fact]
        public async Task Operator_CannotConfirmImport()
        {
            using var client =
                CreateAuthenticatedClient("OPERATOR");

            using var content = CreateMultipartContent(
                [1, 2, 3],
                "sop-rules.xlsx");

            var response = await client.PostAsync(
                "/api/sop-rules/import",
                content);

            Assert.Equal(
                HttpStatusCode.Forbidden,
                response.StatusCode);
        }

        [Fact]
        public async Task PortManager_CannotDownloadImportTemplate()
        {
            using var client =
                CreateAuthenticatedClient("PORT_MANAGER");

            var response = await client.GetAsync(
                "/api/sop-rules/import-template");

            Assert.Equal(
                HttpStatusCode.Forbidden,
                response.StatusCode);
        }

        [Fact]
        public async Task Admin_CreateRuleWithoutTechnicalFields_GeneratesDefaults()
        {
            var ruleCode =
                $"TEST-AUTO-{Guid.NewGuid():N}"[..24].ToUpperInvariant();

            using var client = CreateAuthenticatedClient("ADMIN");

            try
            {
                var response = await client.PostAsJsonAsync(
                    "/api/sop-rules",
                    new
                    {
                        RuleCode = ruleCode,
                        RuleName = "Chuyển sang chế độ hạn chế",
                        Description = "Kiểm tra cấu hình tự động.",
                        TriggerRiskLevel = "HIGH",
                        PreviousRiskLevel = (string?)null,
                        AppliesToZoneType = "DOCK",
                        ActionType = "SET_LIMITED_MODE",
                        IsActive = true,
                        ChangeReason = "Kiểm thử cấu hình tự động"
                    });

                Assert.Equal(HttpStatusCode.Created, response.StatusCode);

                var created = await response.Content
                    .ReadFromJsonAsync<SopRuleResponse>();

                Assert.NotNull(created);
                Assert.Equal(20, created.ExecutionOrder);
                Assert.Equal(
                    "LIMITED",
                    created.ActionConfig
                        .GetProperty("mode")
                        .GetString());
                Assert.True(
                    created.ActionConfig
                        .GetProperty("sendAlert")
                        .GetBoolean());
            }
            finally
            {
                await CleanupImportTestAsync(ruleCode, ruleCode);
            }
        }

        [Fact]
        public async Task Admin_CanDownloadTemplate_AndPreviewSameFile()
        {
            using var client =
                CreateAuthenticatedClient("ADMIN");

            // Tải template được tạo từ cấu hình SOP hiện tại
            var templateResponse = await client.GetAsync(
                "/api/sop-rules/import-template");

            Assert.Equal(
                HttpStatusCode.OK,
                templateResponse.StatusCode);

            Assert.Equal(
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                templateResponse.Content.Headers.ContentType?.MediaType);

            Assert.Contains(
                "PORMS_SopRules_Template.xlsx",
                templateResponse.Content.Headers.ContentDisposition?
                    .ToString() ?? string.Empty);

            var fileBytes =
                await templateResponse.Content.ReadAsByteArrayAsync();

            Assert.NotEmpty(fileBytes);

            int templateDataRows;

            // Xác nhận response thực sự là workbook SOP hợp lệ
            using (var stream = new MemoryStream(fileBytes))
            using (var workbook = new XLWorkbook(stream))
            {
                var worksheet =
                    Assert.Single(workbook.Worksheets);

                Assert.Equal(
                    SopRuleExcelService.WorksheetName,
                    worksheet.Name);

                Assert.Equal(
                    "RuleCode",
                    worksheet.Cell(1, 1).GetString());

                Assert.Equal(
                    "RuleName",
                    worksheet.Cell(1, 2).GetString());

                Assert.Equal(
                    "ActionType",
                    worksheet.Cell(1, 7).GetString());

                Assert.Equal(
                    "IsActive",
                    worksheet.Cell(1, 10).GetString());

                var lastRow = worksheet
                    .LastRowUsed(XLCellsUsedOptions.Contents)?
                    .RowNumber() ?? 1;

                templateDataRows = lastRow - 1;

                Assert.True(
                    templateDataRows > 0,
                    "Database phải có ít nhất một SOP rule.");
            }

            // Gửi lại chính template vừa tải để kiểm tra round-trip
            using var previewContent = CreateMultipartContent(
                fileBytes,
                "PORMS_SopRules_Template.xlsx");

            var previewResponse = await client.PostAsync(
                "/api/sop-rules/import/preview",
                previewContent);

            Assert.Equal(
                HttpStatusCode.OK,
                previewResponse.StatusCode);

            var preview = await previewResponse.Content
                .ReadFromJsonAsync<SopRuleImportPreviewResponse>();

            Assert.NotNull(preview);
            Assert.True(preview.CanImport);
            Assert.Equal(templateDataRows, preview.TotalRows);
            Assert.Equal(templateDataRows, preview.ValidRows);
            Assert.Equal(0, preview.InvalidRows);
            Assert.Equal(0, preview.CreateCount);
            Assert.Equal(0, preview.UpdateCount);
            Assert.Equal(
                templateDataRows,
                preview.UnchangedCount);
        }

        [Fact]
        public async Task Admin_CanCreateAndUpdateRules_InOneImport()
        {
            var suffix = Guid.NewGuid()
                .ToString("N")[..8]
                .ToUpperInvariant();

            var existingCode = $"TEST-SOP-UPD-{suffix}";
            var createdCode = $"TEST-SOP-NEW-{suffix}";
            var fileName = $"PORMS_SopImport_Test_{suffix}.xlsx";

            var admin = await _factory
                .GetFirstActiveUserByRoleAsync("ADMIN");

            var initialRuleCount =
                await CountActiveRulesAsync();

            await SeedExistingRuleAsync(
                existingCode,
                admin.UserId);

            try
            {
                var excelService = _factory.Services
                    .GetRequiredService<SopRuleExcelService>();

                // File chứa một rule cần UPDATE và một rule cần CREATE
                var fileBytes = excelService.CreateTemplate(
                [
                    new ValidatedSopRuleImport(
                RowNumber: 0,
                RuleCode: existingCode,
                RuleName: "SOP kiểm thử cập nhật",
                Description: "Đã cập nhật bằng Excel.",
                TriggerRiskLevel: "MEDIUM",
                PreviousRiskLevel: "LOW",
                AppliesToZoneType: "GATE",
                ActionType: "SEND_ALERT",
                ActionConfigJson:
                    "{\"sendAlert\":true}",
                ExecutionOrder: 901,
                IsActive: true),

            new ValidatedSopRuleImport(
                RowNumber: 0,
                RuleCode: createdCode,
                RuleName: "SOP kiểm thử tạo mới",
                Description: "Được tạo bằng Excel.",
                TriggerRiskLevel: "HIGH",
                PreviousRiskLevel: "MEDIUM",
                AppliesToZoneType: "DOCK",
                ActionType: "CREATE_TASK",
                ActionConfigJson:
                    "{\"title\":\"Kiểm tra SOP import\",\"priority\":\"HIGH\"}",
                ExecutionOrder: 902,
                IsActive: true)
                ]);

                using var client = CreateAuthenticatedClient(
                    "ADMIN",
                    admin.UserId);

                // Preview phải nhận đúng một CREATE và một UPDATE
                using var previewContent =
                    CreateMultipartContent(fileBytes, fileName);

                var previewResponse = await client.PostAsync(
                    "/api/sop-rules/import/preview",
                    previewContent);

                Assert.Equal(
                    HttpStatusCode.OK,
                    previewResponse.StatusCode);

                var preview = await previewResponse.Content
                    .ReadFromJsonAsync<SopRuleImportPreviewResponse>();

                Assert.NotNull(preview);
                Assert.True(preview.CanImport);
                Assert.Equal(2, preview.TotalRows);
                Assert.Equal(1, preview.CreateCount);
                Assert.Equal(1, preview.UpdateCount);
                Assert.Equal(0, preview.UnchangedCount);

                // Xác nhận import bằng chính file đã preview
                using var importContent = CreateMultipartContent(
                    fileBytes,
                    fileName,
                    "Kiểm thử import SOP bằng Excel.");

                var importResponse = await client.PostAsync(
                    "/api/sop-rules/import",
                    importContent);

                Assert.Equal(
                    HttpStatusCode.OK,
                    importResponse.StatusCode);

                var result = await importResponse.Content
                    .ReadFromJsonAsync<SopRuleImportResponse>();

                Assert.NotNull(result);
                Assert.NotEqual(Guid.Empty, result.ImportBatchId);
                Assert.Equal(1, result.CreatedCount);
                Assert.Equal(1, result.UpdatedCount);
                Assert.Equal(0, result.UnchangedCount);

                // Rule tạm đã seed phải được cập nhật và tăng version
                var updatedRule = Assert.Single(
                    result.Configuration.Rules,
                    rule => rule.RuleCode == existingCode);

                Assert.Equal(
                    "Đã cập nhật bằng Excel.",
                    updatedRule.Description);

                Assert.Equal("MEDIUM", updatedRule.TriggerRiskLevel);
                Assert.Equal("LOW", updatedRule.PreviousRiskLevel);
                Assert.Equal("GATE", updatedRule.AppliesToZoneType);
                Assert.Equal((short)901, updatedRule.ExecutionOrder);
                Assert.Equal(2, updatedRule.Version);

                // Rule mới phải được tạo với version đầu tiên
                var createdRule = Assert.Single(
                    result.Configuration.Rules,
                    rule => rule.RuleCode == createdCode);

                Assert.Equal("HIGH", createdRule.TriggerRiskLevel);
                Assert.Equal("CREATE_TASK", createdRule.ActionType);
                Assert.Equal(1, createdRule.Version);

                // Các SOP không có trong file không bị xóa
                Assert.Equal(
                    initialRuleCount + 2,
                    result.Configuration.Summary.TotalRules);

                Assert.True(
                    await ImportAuditExistsAsync(
                        result.ImportBatchId,
                        admin.UserId));
            }
            finally
            {
                // Chỉ xóa dữ liệu SOP tạm.
                // Operation event là audit log append-only nên phải được giữ lại.
                await CleanupImportTestAsync(
                    existingCode,
                    createdCode);
            }
        }

        private HttpClient CreateAuthenticatedClient(
            string role,
            Guid? userId = null)
        {
            var client = _factory.CreateClient();

            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue(
                    "Bearer",
                    CreateToken(role, userId));

            return client;
        }



        /// Tạo multipart request giống request được gửi từ frontend
        private static MultipartFormDataContent CreateMultipartContent(
            byte[] fileBytes,
            string fileName,
            string? changeReason = null)
        {
            var content = new MultipartFormDataContent();
            var fileContent = new ByteArrayContent(fileBytes);

            fileContent.Headers.ContentType =
                new MediaTypeHeaderValue(
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");

            content.Add(fileContent, "File", fileName);

            if (!string.IsNullOrWhiteSpace(changeReason))
            {
                content.Add(
                    new StringContent(changeReason, Encoding.UTF8),
                    "ChangeReason");
            }

            return content;
        }

        private async Task<long> CountActiveRulesAsync()
        {
            await using var connection =
                new NpgsqlConnection(_factory.GetConnectionString());

            await connection.OpenAsync();

            const string sql = """
                SELECT COUNT(*)
                FROM operational.sop_rules
                WHERE deleted_at IS NULL;
                """;

            await using var command =
                new NpgsqlCommand(sql, connection);

            return (long)(await command.ExecuteScalarAsync())!;
        }

        private async Task SeedExistingRuleAsync(
            string ruleCode,
            Guid adminUserId)
        {
            await using var connection =
                new NpgsqlConnection(_factory.GetConnectionString());

            await connection.OpenAsync();

            const string sql = """
                INSERT INTO operational.sop_rules (
                    rule_code, rule_name, description,
                    trigger_risk_level, action_type,
                    action_config, execution_order,
                    is_active, version, change_reason,
                    created_by_user_id, updated_by_user_id
                )
                VALUES (
                    @ruleCode, 'SOP trước khi import',
                    'Dữ liệu ban đầu.',
                    'LOW'::operational.risk_level_enum,
                    'SEND_ALERT'::operational.sop_action_type_enum,
                    @actionConfig, 900, TRUE, 1,
                    'Chuẩn bị integration test.',
                    @adminUserId, @adminUserId
                );
                """;

            await using var command =
                new NpgsqlCommand(sql, connection);

            command.Parameters.AddWithValue("ruleCode", ruleCode);

            command.Parameters.Add(
                "actionConfig",
                NpgsqlDbType.Jsonb).Value =
                "{\"sendAlert\":false}";

            command.Parameters.AddWithValue(
                "adminUserId",
                adminUserId);

            await command.ExecuteNonQueryAsync();
        }

        private async Task<bool> ImportAuditExistsAsync(
            Guid importBatchId,
            Guid adminUserId)
        {
            await using var connection =
                new NpgsqlConnection(_factory.GetConnectionString());

            await connection.OpenAsync();

            const string sql = """
                SELECT EXISTS (
                    SELECT 1
                    FROM operational.operation_events
                    WHERE event_type = 'SOP_RULES_IMPORTED'
                        AND entity_id = @importBatchId
                        AND correlation_id = @importBatchId
                        AND actor_user_id = @adminUserId
                );
                """;

            await using var command =
                new NpgsqlCommand(sql, connection);

            command.Parameters.AddWithValue(
                "importBatchId",
                importBatchId);

            command.Parameters.AddWithValue(
                "adminUserId",
                adminUserId);

            return (bool)(await command.ExecuteScalarAsync())!;
        }

        private async Task CleanupImportTestAsync(
            string existingCode,
            string createdCode)
        {
            await using var connection =
                new NpgsqlConnection(_factory.GetConnectionString());

            await connection.OpenAsync();

            // Không xóa operation_events vì đây là bảng audit append-only
            const string sql = """
                DELETE FROM operational.sop_rules
                WHERE rule_code IN (@existingCode, @createdCode);
                """;

            await using var command =
                new NpgsqlCommand(sql, connection);

            command.Parameters.AddWithValue(
                "existingCode",
                existingCode);

            command.Parameters.AddWithValue(
                "createdCode",
                createdCode);

            await command.ExecuteNonQueryAsync();
        }

        /// Tạo JWT bằng đúng cấu hình của API
        private string CreateToken(
            string role,
            Guid? userId = null)
        {
            var jwtOptions = _factory.Services
                .GetRequiredService<IOptions<JwtOptions>>()
                .Value;

            var key = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtOptions.SigningKey));

            var credentials = new SigningCredentials(
                key,
                SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(
                    JwtRegisteredClaimNames.Sub,
                    (userId ?? Guid.NewGuid()).ToString()),

                new Claim(
                    JwtRegisteredClaimNames.Email,
                    $"{role.ToLowerInvariant()}@porms.vn"),

                new Claim(ClaimTypes.Name, role),
                new Claim(ClaimTypes.Role, role),

                new Claim(
                    JwtRegisteredClaimNames.Jti,
                    Guid.NewGuid().ToString())
            };

            var token = new JwtSecurityToken(
                issuer: jwtOptions.Issuer,
                audience: jwtOptions.Audience,
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(15),
                signingCredentials: credentials);

            return new JwtSecurityTokenHandler()
                .WriteToken(token);
        }
    }
}
