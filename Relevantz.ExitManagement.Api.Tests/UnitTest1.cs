using NUnit.Framework;
using Moq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using Relevantz.ExitManagement.Api.Controllers;
using Relevantz.ExitManagement.Core.IService;
using Relevantz.ExitManagement.Common.DTOs;

namespace Relevantz.ExitManagement.Api.Tests.Controllers
{
    [TestFixture]
    public class ExitControllerTests
    {
        private Mock<IExitService> _mockExitService;
        private Mock<IDocumentService> _mockDocumentService;
        private ExitController _controller;

        [SetUp]
        public void Setup()
        {
            _mockExitService = new Mock<IExitService>();
            _mockDocumentService = new Mock<IDocumentService>();

            _controller = new ExitController(
                _mockExitService.Object,
                _mockDocumentService.Object
            );

            var httpContext = new DefaultHttpContext();

            var claims = new List<Claim>
            {
                new Claim("empId","1"),
                new Claim(ClaimTypes.Role,"HR")
            };

            var identity = new ClaimsIdentity(claims,"TestAuth");

            httpContext.User = new ClaimsPrincipal(identity);

            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };
        }

        // ──────────────────────────────────────────────
        // SubmitResignation
        // ──────────────────────────────────────────────

        [Test]
        public async Task SubmitResignation_WithValidRequest_ReturnsOkResult()
        {
            var dto = new SubmitResignationRequestDto();

            _mockExitService
                .Setup(s => s.SubmitResignationAsync(1, dto))
                .ReturnsAsync(100);

            var result = await _controller.SubmitResignation(dto);

            Assert.That(result, Is.InstanceOf<OkObjectResult>());
        }

        [Test]
        public async Task SubmitResignation_VerifiesServiceCalledOnce()
        {
            var dto = new SubmitResignationRequestDto();

            _mockExitService
                .Setup(s => s.SubmitResignationAsync(It.IsAny<int>(), dto))
                .ReturnsAsync(1);

            await _controller.SubmitResignation(dto);

            _mockExitService.Verify(
                s => s.SubmitResignationAsync(It.IsAny<int>(), dto),
                Times.Once);
        }

        [Test]
        public async Task SubmitResignation_PassesCorrectEmployeeId()
        {
            var dto = new SubmitResignationRequestDto();
            int capturedEmpId = 0;

            _mockExitService
                .Setup(s => s.SubmitResignationAsync(It.IsAny<int>(), dto))
                .Callback<int, SubmitResignationRequestDto>((id, _) =>
                    capturedEmpId = id)
                .ReturnsAsync(1);

            await _controller.SubmitResignation(dto);

            Assert.That(capturedEmpId, Is.EqualTo(1));
        }

        // ──────────────────────────────────────────────
        // Manager Approval
        // ──────────────────────────────────────────────

        [Test]
        public async Task ManagerApproval_WithValidRequest_ReturnsOk()
        {
            var dto = new ManagerApprovalRequestDto();

            _mockExitService
                .Setup(s => s.ManagerApproveAsync(It.IsAny<int>(), dto))
                .Returns(Task.CompletedTask);

            var result = await _controller.ManagerApproval(dto);

            Assert.That(result, Is.InstanceOf<OkObjectResult>());
        }

        [Test]
        public async Task ManagerApproval_VerifiesServiceCalledOnce()
        {
            var dto = new ManagerApprovalRequestDto();

            _mockExitService
                .Setup(s => s.ManagerApproveAsync(It.IsAny<int>(), dto))
                .Returns(Task.CompletedTask);

            await _controller.ManagerApproval(dto);

            _mockExitService.Verify(
                s => s.ManagerApproveAsync(It.IsAny<int>(), dto),
                Times.Once);
        }

        // ──────────────────────────────────────────────
        // HR Approval
        // ──────────────────────────────────────────────

        [Test]
        public async Task HrApproval_WithValidRequest_ReturnsOk()
        {
            var dto = new HrApprovalRequestDto();

            _mockExitService
                .Setup(s => s.HrApproveAsync(It.IsAny<int>(), dto))
                .Returns(Task.CompletedTask);

            var result = await _controller.HrApproval(dto);

            Assert.That(result, Is.InstanceOf<OkObjectResult>());
        }

        [Test]
        public async Task HrApproval_VerifiesServiceCalledOnce()
        {
            var dto = new HrApprovalRequestDto();

            _mockExitService
                .Setup(s => s.HrApproveAsync(It.IsAny<int>(), dto))
                .Returns(Task.CompletedTask);

            await _controller.HrApproval(dto);

            _mockExitService.Verify(
                s => s.HrApproveAsync(It.IsAny<int>(), dto),
                Times.Once);
        }

        // ──────────────────────────────────────────────
        // Update Clearance
        // ──────────────────────────────────────────────

        [Test]
        public async Task UpdateClearance_WithValidRequest_ReturnsOk()
        {
            var dto = new UpdateClearanceRequestDto();

            _mockExitService
                .Setup(s => s.UpdateClearanceAsync(It.IsAny<int>(), dto))
                .Returns(Task.CompletedTask);

            var result = await _controller.UpdateClearance(dto);

            Assert.That(result, Is.InstanceOf<OkObjectResult>());
        }

        [Test]
        public async Task UpdateClearance_VerifiesServiceCalledOnce()
        {
            var dto = new UpdateClearanceRequestDto();

            _mockExitService
                .Setup(s => s.UpdateClearanceAsync(It.IsAny<int>(), dto))
                .Returns(Task.CompletedTask);

            await _controller.UpdateClearance(dto);

            _mockExitService.Verify(
                s => s.UpdateClearanceAsync(It.IsAny<int>(), dto),
                Times.Once);
        }

        // ──────────────────────────────────────────────
        // Get Clearance Items
        // ──────────────────────────────────────────────

        [Test]
        public async Task GetClearanceItems_ReturnsOkResult()
        {
            int exitId = 1;
            string dept = "IT";

            _mockExitService
                .Setup(s => s.GetClearanceItemsAsync(exitId, dept))
                .ReturnsAsync(new List<ClearanceItemResponseDto>());

            var result = await _controller.GetClearanceItems(exitId, dept);

            Assert.That(result, Is.InstanceOf<OkObjectResult>());
        }

        [Test]
        public async Task GetClearanceItems_VerifiesServiceCalled()
        {
            int exitId = 5;
            string dept = "HR";

            _mockExitService
                .Setup(s => s.GetClearanceItemsAsync(exitId, dept))
                .ReturnsAsync(new List<ClearanceItemResponseDto>());

            await _controller.GetClearanceItems(exitId, dept);

            _mockExitService.Verify(
                s => s.GetClearanceItemsAsync(exitId, dept),
                Times.Once);
        }

        // ──────────────────────────────────────────────
        // Get Declared Assets
        // ──────────────────────────────────────────────

        [Test]
        public async Task GetDeclaredAssets_ReturnsOkResult()
        {
            int exitId = 2;

            _mockExitService
                .Setup(s => s.GetAssetsByExitIdAsync(exitId))
                .ReturnsAsync(new List<AssetDeclarationDto>());

            var result = await _controller.GetDeclaredAssets(exitId);

            Assert.That(result, Is.InstanceOf<OkObjectResult>());
        }

        [Test]
        public async Task GetDeclaredAssets_VerifiesServiceCalled()
        {
            int exitId = 3;

            _mockExitService
                .Setup(s => s.GetAssetsByExitIdAsync(exitId))
                .ReturnsAsync(new List<AssetDeclarationDto>());

            await _controller.GetDeclaredAssets(exitId);

            _mockExitService.Verify(
                s => s.GetAssetsByExitIdAsync(exitId),
                Times.Once);
        }

        // ──────────────────────────────────────────────
        // GetMyExitStatus
        // ──────────────────────────────────────────────

        [Test]
        public async Task GetMyExitStatus_ReturnsOkResult()
        {
            _mockExitService
                .Setup(s => s.GetMyExitStatusAsync(It.IsAny<int>()))
                .ReturnsAsync(new ExitStatusResponseDto());

            var result = await _controller.GetMyExitStatus();

            Assert.That(result, Is.InstanceOf<OkObjectResult>());
        }

        [Test]
        public async Task GetMyExitStatus_VerifiesServiceCalled()
        {
            _mockExitService
                .Setup(s => s.GetMyExitStatusAsync(It.IsAny<int>()))
                .ReturnsAsync(new ExitStatusResponseDto());

            await _controller.GetMyExitStatus();

            _mockExitService.Verify(
                s => s.GetMyExitStatusAsync(It.IsAny<int>()),
                Times.Once);
        }

        // ──────────────────────────────────────────────
        // GetAllRequests
        // ──────────────────────────────────────────────

        [Test]
        public async Task GetAllRequests_ReturnsOkResult()
        {
            _mockExitService
                .Setup(s => s.GetAllExitRequestsAsync(null))
                .ReturnsAsync(new List<ExitRequestSummaryDto>());

            var result = await _controller.GetAllRequests();

            Assert.That(result, Is.InstanceOf<OkObjectResult>());
        }

        [Test]
        public async Task GetAllRequests_VerifiesServiceCalled()
        {
            _mockExitService
                .Setup(s => s.GetAllExitRequestsAsync(It.IsAny<string>()))
                .ReturnsAsync(new List<ExitRequestSummaryDto>());

            await _controller.GetAllRequests();

            _mockExitService.Verify(
                s => s.GetAllExitRequestsAsync(It.IsAny<string>()),
                Times.Once);
        }

        // ──────────────────────────────────────────────
        // GetRequestById
        // ──────────────────────────────────────────────

        [Test]
        public async Task GetRequestById_ReturnsOkResult()
        {
            int id = 10;

            _mockExitService
                .Setup(s => s.GetExitRequestByIdAsync(id))
                .ReturnsAsync(new ExitRequestSummaryDto());

            var result = await _controller.GetRequestById(id);

            Assert.That(result, Is.InstanceOf<OkObjectResult>());
        }

        [Test]
        public async Task GetRequestById_VerifiesServiceCalledOnce()
        {
            int id = 4;

            _mockExitService
                .Setup(s => s.GetExitRequestByIdAsync(id))
                .ReturnsAsync(new ExitRequestSummaryDto());

            await _controller.GetRequestById(id);

            _mockExitService.Verify(
                s => s.GetExitRequestByIdAsync(id),
                Times.Once);
        }
    }
}