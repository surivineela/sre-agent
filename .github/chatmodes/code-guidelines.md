1. Use WebJsonSerializer instead of raw JsonSerializer

2. No mocking at all.. Use of Moq is prohibited. Everything should connect to real app.

3. The tests refer to Agent.Web and use its appsettings file.

4. Use public sealed classes and records where possible.