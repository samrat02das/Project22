namespace OrderIntake;

public class ValidationError
{
	public string Field { get; set; } = string.Empty;
	public string Code { get; set; } = string.Empty;
	public string Message { get; set; } = string.Empty;
}

public class Order
{
	public string OrderId { get; set; } = string.Empty;
	public string PatientId { get; set; } = string.Empty;
	public string SpecimenId { get; set; } = string.Empty;
	public string SpecimenType { get; set; } = string.Empty;
	public string Priority { get; set; } = string.Empty;
	public DateTime CollectionDate { get; set; }
	public List<string> RequestedTests { get; set; } = new();
}

public class OrderResult
{
	public string Status { get; set; } = string.Empty;
	public Order? Order { get; set; }
	public List<ValidationError> Errors { get; set; } = new();
}