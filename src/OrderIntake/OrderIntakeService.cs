using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OrderIntake;

public class OrderIntakeService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    static OrderIntakeService()
    {
        JsonOptions.Converters.Add(new LenientDateTimeConverter());
    }

    public OrderResult Process(string json)
    {
        Order order;
        JsonDocument document;

        try
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new JsonException();
            }

            order = JsonSerializer.Deserialize<Order>(json, JsonOptions)
                ?? throw new JsonException();
            document = JsonDocument.Parse(json);
        }
        catch (Exception)
        {
            return Rejected("$", "MALFORMED_INPUT", "Invalid JSON");
        }

        using (document)
        {
            var errors = new List<ValidationError>();

            ValidateId(order.OrderId, nameof(Order.OrderId), errors);
            ValidateId(order.PatientId, nameof(Order.PatientId), errors);
            ValidateId(order.SpecimenId, nameof(Order.SpecimenId), errors);
            ValidateSpecimenType(order, errors);
            ValidatePriority(order, errors);
            ValidateCollectionDate(order, document.RootElement, errors);
            ValidateRequestedTests(order.RequestedTests, errors);

            return errors.Count > 0
                ? new OrderResult { Status = "Rejected", Order = null, Errors = errors }
                : new OrderResult { Status = "Accepted", Order = order, Errors = new() };
        }
    }

    private static void ValidateId(string? value, string field, List<ValidationError> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add(Error(field, "REQUIRED", $"{field} is required."));
        }

        if (value is not null && value.Length > 20)
        {
            errors.Add(Error(field, "MAX_LENGTH", $"{field} must be 20 characters or fewer."));
        }
    }

    private static void ValidateSpecimenType(Order order, List<ValidationError> errors)
    {
        if (string.IsNullOrWhiteSpace(order.SpecimenType))
        {
            errors.Add(Error(nameof(Order.SpecimenType), "REQUIRED", "SpecimenType is required."));
            return;
        }

        var normalized = NormalizeValue(order.SpecimenType, "Blood", "Urine", "Tissue", "Saliva");
        if (normalized is null)
        {
            errors.Add(Error(nameof(Order.SpecimenType), "INVALID_VALUE", "SpecimenType is invalid."));
            return;
        }

        order.SpecimenType = normalized;
    }

    private static void ValidatePriority(Order order, List<ValidationError> errors)
    {
        if (string.IsNullOrWhiteSpace(order.Priority))
        {
            errors.Add(Error(nameof(Order.Priority), "REQUIRED", "Priority is required."));
            return;
        }

        var normalized = NormalizeValue(order.Priority, "Routine", "Urgent");
        if (normalized is null)
        {
            errors.Add(Error(nameof(Order.Priority), "INVALID_VALUE", "Priority is invalid."));
            return;
        }

        order.Priority = normalized;
    }

    private static void ValidateCollectionDate(Order order, JsonElement root, List<ValidationError> errors)
    {
        var dateProperty = FindProperty(root, nameof(Order.CollectionDate));
        if (dateProperty is null || dateProperty.Value.ValueKind is JsonValueKind.Null)
        {
            errors.Add(Error(nameof(Order.CollectionDate), "REQUIRED", "CollectionDate is required."));
            return;
        }

        if (dateProperty.Value.ValueKind != JsonValueKind.String ||
            !DateTime.TryParseExact(
                dateProperty.Value.GetString(),
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var collectionDate))
        {
            errors.Add(Error(nameof(Order.CollectionDate), "INVALID_FORMAT", "CollectionDate must use yyyy-MM-dd format."));
            return;
        }

        order.CollectionDate = collectionDate;
        if (collectionDate.Date > DateTime.Today)
        {
            errors.Add(Error(nameof(Order.CollectionDate), "FUTURE_DATE", "CollectionDate cannot be in the future."));
        }
    }

    private static void ValidateRequestedTests(List<string>? requestedTests, List<ValidationError> errors)
    {
        if (requestedTests is null || requestedTests.Count == 0)
        {
            errors.Add(Error(nameof(Order.RequestedTests), "REQUIRED", "RequestedTests is required."));
            return;
        }

        for (var index = 0; index < requestedTests.Count; index++)
        {
            if (string.IsNullOrWhiteSpace(requestedTests[index]))
            {
                errors.Add(Error($"{nameof(Order.RequestedTests)}[{index}]", "INVALID_VALUE", "Requested test is invalid."));
            }
        }

        var distinctCount = requestedTests
            .Where(test => !string.IsNullOrWhiteSpace(test))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();
        if (distinctCount != requestedTests.Count(test => !string.IsNullOrWhiteSpace(test)))
        {
            errors.Add(Error(nameof(Order.RequestedTests), "DUPLICATE", "RequestedTests contains duplicates."));
        }
    }

    private static string? NormalizeValue(string value, params string[] allowedValues)
    {
        return allowedValues.FirstOrDefault(allowed =>
            string.Equals(value, allowed, StringComparison.OrdinalIgnoreCase));
    }

    private static JsonElement? FindProperty(JsonElement root, string propertyName)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        JsonElement? lastMatch = null;
        foreach (var property in root.EnumerateObject())
        {
            if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                lastMatch = property.Value;
            }
        }

        return lastMatch;
    }

    private static ValidationError Error(string field, string code, string message)
    {
        return new ValidationError { Field = field, Code = code, Message = message };
    }

    private static OrderResult Rejected(string field, string code, string message)
    {
        return new OrderResult
        {
            Status = "Rejected",
            Order = null,
            Errors = new List<ValidationError> { Error(field, code, message) }
        };
    }

    private sealed class LenientDateTimeConverter : JsonConverter<DateTime>
    {
        public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
            {
                return default;
            }

            if (reader.TokenType != JsonTokenType.String)
            {
                throw new JsonException();
            }

            return DateTime.TryParse(
                reader.GetString(),
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var value)
                ? value
                : default;
        }

        public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value);
        }
    }
}