using ClosedXML.Excel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using PORMS.API.Configuration;
using PORMS.API.Services;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Xunit;

namespace PORMS.Tests.Integration
{
    /// Kiểm tra quyền truy cập và luồng tải template Excel ngưỡng rủi ro
    /// Test tải thành công sử dụng cấu hình threshold thật trong database
    [Collection(DatabaseBackedIntegrationCollection.Name)]
    public sealed class RiskThresholdImportAuthorizationTests
    {
        private readonly IntegrationTestWebApplicationFactory _factory;

        public RiskThresholdImportAuthorizationTests(
            IntegrationTestWebApplicationFactory factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task UnauthenticatedUser_CannotPreviewImport()
        {
            using var client = _factory.CreateClient();
            using var content = CreateMultipartContent();

            var response = await client.PostAsync(
                "/api/risk/thresholds/import/preview",
                content);

            Assert.Equal(
                HttpStatusCode.Unauthorized,
                response.StatusCode);
        }

        [Fact]
        public async Task PortManager_CannotPreviewImport()
        {
            using var client = CreateAuthenticatedClient("PORT_MANAGER");
            using var content = CreateMultipartContent();

            var response = await client.PostAsync(
                "/api/risk/thresholds/import/preview",
                content);

            Assert.Equal(
                HttpStatusCode.Forbidden,
                response.StatusCode);
        }

        [Fact]
        public async Task Operator_CannotConfirmImport()
        {
            using var client = CreateAuthenticatedClient("OPERATOR");
            using var content = CreateMultipartContent();

            var response = await client.PostAsync(
                "/api/risk/thresholds/import",
                content);

            Assert.Equal(
                HttpStatusCode.Forbidden,
                response.StatusCode);
        }

        [Fact]
        public async Task PortManager_CannotDownloadImportTemplate()
        {
            using var client = CreateAuthenticatedClient("PORT_MANAGER");

            var response = await client.GetAsync(
                "/api/risk/thresholds/import-template");

            Assert.Equal(
                HttpStatusCode.Forbidden,
                response.StatusCode);
        }

        private HttpClient CreateAuthenticatedClient(string role)
        {
            var client = _factory.CreateClient();

            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue(
                    "Bearer",
                    CreateToken(role));

            return client;
        }

        /// Tạo request multipart tối thiểu.
        /// Nội dung file không cần hợp lệ vì request sẽ bị chặn trước khi đọc file.
        private static MultipartFormDataContent CreateMultipartContent()
        {
            var content = new MultipartFormDataContent();
            var fileContent = new ByteArrayContent([1, 2, 3]);

            content.Add(
                fileContent,
                "File",
                "risk-thresholds.xlsx");

            return content;
        }

        /// Tạo JWT bằng đúng cấu hình mà API đang sử dụng
        /// Tránh test bị lỗi khi Docker hoặc environment thay đổi JWT settings
        private string CreateToken(string role)
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
                Guid.NewGuid().ToString()),

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

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
        [Fact]
        public async Task Admin_CanDownloadImportTemplate()
        {
            using var client = CreateAuthenticatedClient("ADMIN");

            var response = await client.GetAsync(
                "/api/risk/thresholds/import-template");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            Assert.Equal(
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                response.Content.Headers.ContentType?.MediaType);

            Assert.Contains(
                "PORMS_RiskThresholds_Template.xlsx",
                response.Content.Headers.ContentDisposition?.ToString()
                    ?? string.Empty);

            var fileContent = await response.Content.ReadAsByteArrayAsync();

            Assert.NotEmpty(fileContent);

            // Mở file thật để xác nhận response là workbook hợp lệ
            using var stream = new MemoryStream(fileContent);
            using var workbook = new XLWorkbook(stream);

            var worksheet = Assert.Single(workbook.Worksheets);

            Assert.Equal(
                RiskThresholdExcelService.WorksheetName,
                worksheet.Name);

            Assert.Equal("Factor", worksheet.Cell(1, 1).GetString());
            Assert.Equal("RiskLevel", worksheet.Cell(1, 2).GetString());
            Assert.Equal(
                "ComparisonOperator",
                worksheet.Cell(1, 3).GetString());

            // Một dòng header và 12 threshold của cấu hình Version 1
            Assert.Equal(
                13,
                worksheet
                    .LastRowUsed(XLCellsUsedOptions.Contents)?
                    .RowNumber());
        }
    }
}
