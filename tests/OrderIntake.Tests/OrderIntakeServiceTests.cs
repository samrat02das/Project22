using Xunit;

namespace OrderIntake;

public class OrderIntakeServiceTests
{
	private readonly OrderIntakeService service = new();

	[Fact]
	public void Process_ValidOrder_AcceptsAndNormalizesValues()
	{
		var result = service.Process("""
			{
			  "orderId": "ORD-123",
			  "patientId": "PAT-456",
			  "specimenId": "SP-789",
			  "specimenType": "bLoOd",
			  "priority": "uRgEnT",
			  "collectionDate": "2026-09-20",
			  "requestedTests": ["CBC", "Glucose"],
			  "unknownField": "ignored"
			}
			""");

		Assert.Equal("Accepted", result.Status);
		Assert.NotNull(result.Order);
		Assert.Equal("Blood", result.Order!.SpecimenType);
		Assert.Equal("Urgent", result.Order.Priority);
		Assert.Equal(new[] { "CBC", "Glucose" }, result.Order.RequestedTests);
		Assert.Empty(result.Errors);
	}

	[Fact]
	public void Process_InvalidOrder_ReturnsAllErrors()
	{
		var result = service.Process("""
			{
			  "orderId": "   ",
			  "patientId": "PAT-456",
			  "specimenId": "SP-789",
			  "specimenType": "Plasma",
			  "priority": "Immediate",
			  "collectionDate": "2026/09/20",
			  "requestedTests": []
			}
			""");

		Assert.Equal("Rejected", result.Status);
		Assert.Null(result.Order);
		Assert.Equal(
			new[]
			{
				(nameof(Order.OrderId), "REQUIRED"),
				(nameof(Order.SpecimenType), "INVALID_VALUE"),
				(nameof(Order.Priority), "INVALID_VALUE"),
				(nameof(Order.CollectionDate), "INVALID_FORMAT"),
				(nameof(Order.RequestedTests), "REQUIRED")
			},
			result.Errors.Select(error => (error.Field, error.Code)));
	}

	[Theory]
	[InlineData("12345678901234567890", true)]
	[InlineData("123456789012345678901", false)]
	public void Process_OrderIdLength_EnforcesMaximumLength(string orderId, bool shouldBeAccepted)
	{
		var result = service.Process(CreateValidJson($"\"orderId\":\"{orderId}\""));

		Assert.Equal(shouldBeAccepted ? "Accepted" : "Rejected", result.Status);
		Assert.Equal(shouldBeAccepted, result.Errors.All(error => error.Code != "MAX_LENGTH"));
		if (!shouldBeAccepted)
		{
			Assert.Contains(result.Errors, error =>
				error.Field == nameof(Order.OrderId) && error.Code == "MAX_LENGTH");
		}
	}

	[Theory]
	[InlineData("2026-02-30")]
	[InlineData("2026/09/20")]
	public void Process_InvalidCollectionDate_RejectsInput(string collectionDate)
	{
		var result = service.Process(CreateValidJson($"\"collectionDate\":\"{collectionDate}\""));

		Assert.Equal("Rejected", result.Status);
		Assert.Contains(result.Errors, error =>
			error.Field == nameof(Order.CollectionDate) && error.Code == "INVALID_FORMAT");
	}

	[Fact]
	public void Process_TomorrowsCollectionDate_ReturnsFutureDateError()
	{
		var tomorrow = DateTime.Today.AddDays(1).ToString("yyyy-MM-dd");
		var result = service.Process(CreateValidJson($"\"collectionDate\":\"{tomorrow}\""));

		Assert.Equal("Rejected", result.Status);
		Assert.Contains(result.Errors, error =>
			error.Field == nameof(Order.CollectionDate) && error.Code == "FUTURE_DATE");
	}

	[Fact]
	public void Process_EmptyRequestedTests_ReturnsRequiredError()
	{
		var result = service.Process(CreateValidJson("\"requestedTests\":[]"));

		Assert.Equal("Rejected", result.Status);
		Assert.Contains(result.Errors, error =>
			error.Field == nameof(Order.RequestedTests) && error.Code == "REQUIRED");
	}

	[Fact]
	public void Process_DuplicateRequestedTests_ReturnsOneDuplicateError()
	{
		var result = service.Process(CreateValidJson("\"requestedTests\":[\"Glucose\",\"glucose\"]"));

		Assert.Equal("Rejected", result.Status);
		Assert.Equal(1, result.Errors.Count(error => error.Code == "DUPLICATE"));
	}

	[Fact]
	public void Process_BrokenJson_ReturnsMalformedInputError()
	{
		var result = service.Process("{ broken }");

		Assert.Equal("Rejected", result.Status);
		Assert.Null(result.Order);
		var error = Assert.Single(result.Errors);
		Assert.Equal("$", error.Field);
		Assert.Equal("MALFORMED_INPUT", error.Code);
		Assert.Equal("Invalid JSON", error.Message);
	}

	private static string CreateValidJson(string overrideProperty)
	{
		return $$"""
			{
			  "orderId": "ORD-123",
			  "patientId": "PAT-456",
			  "specimenId": "SP-789",
			  "specimenType": "Blood",
			  "priority": "Routine",
			  "collectionDate": "2026-09-20",
			  "requestedTests": ["CBC"],
			  {{overrideProperty}}
			}
			""";
	}
}
