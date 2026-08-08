using Microsoft.AspNetCore.Mvc;
using PORMS.API.Contracts;
using PORMS.API.Controllers;
using Xunit;

namespace PORMS.Tests.Unit;

public sealed class ForecastEvaluationControllerTests
{
    [Fact]
    public void InterventionDemo_IsSeparatedAndTriggersReview()
    {
        var controller = new ForecastEvaluationController();

        var result = controller.GetInterventionDemo();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ForecastEvaluationResponse>(ok.Value);
        Assert.True(response.IsDemonstration);
        Assert.Equal(5, response.Rows.Count);
        Assert.Equal(40m, response.Summary.ConfidencePct);
        Assert.Equal(3, response.Summary.ConsecutiveMismatchCount);
        Assert.Equal(3, response.Summary.DangerousUnderestimateCount);
        Assert.True(response.Summary.InterventionRequired);
        Assert.Contains("không lưu database", response.DataNotice);
    }
}
