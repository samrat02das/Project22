# Order Intake Validation Service

## Overview
This project is a C# class library designed to process and validate incoming medical laboratory JSON orders against strict business rules. It enforces data integrity, handles malformed payloads gracefully, and returns standardized Accepted/Rejected results.

## Setup & Execution
This project was built using the .NET 10 SDK.
- **To execute the test suite:** Open a terminal in the root `Project22` directory and run:
  `dotnet test tests\OrderIntake.Tests`

## AI Usage & Learning Process (JavaScript to C# Transition)
As a JavaScript/Node.js developer, I utilized GitHub Copilot as an interactive learning partner to scaffold C# architectures and translate my Node.js logic into strongly-typed C# patterns. 

**Key areas where AI assisted my workflow:**
1. **Data Modeling (POCOs):** I used Copilot to translate my typical TypeScript interfaces into C# classes, learning how to properly implement modern C# null-safety (`<Nullable>enable</Nullable>`) and property initialization.
2. **JSON Serialization:** Instead of my usual `JSON.parse()`, Copilot introduced me to `System.Text.Json`. Notably, it suggested a clever `LenientDateTimeConverter` to prevent the deserializer from crashing on invalid date strings, which allowed my service to catch those strings manually and return the required `INVALID_FORMAT` business error rather than a generic malformed exception.
3. **xUnit Testing:** I asked Copilot for the C# equivalent of a Jest test suite. It generated standard `[Fact]` and `[Theory]` attributes. When I noticed the tests were masking a date validation error due to how the JSON string was constructed, I was able to manually step in, debug the C# code, and update the DOM traversal to read the last matched property, successfully turning the tests green.
