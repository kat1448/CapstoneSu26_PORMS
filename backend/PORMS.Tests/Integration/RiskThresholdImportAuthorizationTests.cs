using Microsoft.IdentityModel.Tokens;
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
    /// Kiểm tra quyền truy cập các API import ngưỡng rủi ro.
    /// Các test này bị chặn tại Authorization nên không cần kết nối database.
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

        private static string CreateToken(string role)
        {
            var key = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(
                    "PORMS-development-signing-key-change-in-production-2026"));

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
                issuer: "PORMS",
                audience: "PORMS.Frontend",
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(15),
                signingCredentials: credentials);

            return new JwtSecurityTokenHandler()
                .WriteToken(token);
        }
    }
}
