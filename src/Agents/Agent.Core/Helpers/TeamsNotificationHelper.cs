using System.Text;
using System.Text.Json;

namespace Agents.Core.Helpers;

public static class TeamsNotificationHelper
{
    private const string appURI = "";
    /// <summary>
    /// Sends a notification message to Teams via the Azure Logic App endpoint.
    /// </summary>
    /// <param name="httpClient">The HttpClient to use for sending the request.</param>
    /// <param name="logicAppUri">The Azure Logic App endpoint URI.</param>
    /// <param name="messageContent">The text you want to send in the Teams message.</param>
    public static async Task SendTeamsNotificationAsync(
        HttpClient httpClient,
        string messageContent)
    {
        if (string.IsNullOrEmpty(appURI))
        {
            return;
        }

        // Build the payload
        var payload = new
        {
            type = "notification",
            properties = new
            {
                message = new
                {
                    type = "info",
                    description = messageContent
                },
                createdTime = new
                {
                    type = "timestamp",
                    format = "ISO 8601",
                    description = DateTime.UtcNow.ToString("O")
                }
            },
            required = new[] { "message", "createdTime" }
        };

        // Serialize payload to JSON
        var jsonPayload = JsonSerializer.Serialize(payload);
        var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

        // Send HTTP POST request
        try
        {
            var response = await httpClient.PostAsync(appURI, content);
            if (response.IsSuccessStatusCode)
            {
                //Console.WriteLine("Assistant > Successfully sent the message to Logic App.");
            }
            else
            {
                // Console.WriteLine($"Assistant > Failed to send the message. Status code: {response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Assistant > Error occurred while sending the message: {ex.Message}");
        }
    }
}
