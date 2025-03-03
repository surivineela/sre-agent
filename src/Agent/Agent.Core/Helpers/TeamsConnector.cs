// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Microsoft.Extensions.Configuration;
using System.Text;
using System.Threading.Tasks;
using static SkiaSharp.HarfBuzz.SKShaper;
using System.Text.Json;
using Agent.Core.Models;
using Agent.Core.Configuration;

namespace Agent.Core.Helpers;

public sealed class TeamsConnector

{
    private readonly string _endpoint;
    private readonly HttpClient _httpClient;

    public TeamsConnector(ExternalSettings externalSettings)
    {
        _endpoint = externalSettings.TeamsEndpoint;
        _httpClient = new HttpClient();
    }

    public async Task<bool> PostMessageAsync(TeamsMessage teamsMessage)
    {
        if (teamsMessage == null || string.IsNullOrWhiteSpace(teamsMessage.Content)) return false;

        if (string.IsNullOrEmpty(_endpoint))
            return false;
        
        var debugBuilder = new StringBuilder();

        Console.WriteLine(teamsMessage.Content);
        debugBuilder.AppendLine(teamsMessage.Content);
        debugBuilder.AppendLine("------------------------------");

        // Ask Paul about this one weird trick to fix teams formatting: add two blank lines at the beginning.
        teamsMessage.Content = $"{ Environment.NewLine}{ Environment.NewLine}{teamsMessage.Content.Replace("\"", "")}";
        debugBuilder.AppendLine(teamsMessage.Content);
        debugBuilder.AppendLine("------------------------------");

        var payload = new
        {
            message = teamsMessage
        };

        var options = new JsonSerializerOptions { WriteIndented = true };
        var requestBody = JsonSerializer.Serialize(payload, options);
        debugBuilder.AppendLine(requestBody);
        debugBuilder.AppendLine("------------------------------");

        var tempPath = Path.GetTempPath();
        var debugFilePath = Path.Combine(tempPath, "OperationsAgent", $"TeamsMessage_{DateTime.Now:yyyy_MM_dd_hh_mm_ss}.txt");

        try
        {
            Directory.CreateDirectory(Path.Combine(tempPath, "OperationsAgent"));
            await File.WriteAllTextAsync(debugFilePath, debugBuilder.ToString());
        }
        catch (Exception)
        {
            Console.WriteLine($"Failed to write teams message output file");
        }

        var response = await _httpClient.PostAsync(_endpoint, new StringContent(requestBody, Encoding.UTF8, "application/json"));

        if (response.IsSuccessStatusCode)
        {
            try
            {
                var runId = response.Headers.GetValues("x-ms-workflow-run-id").FirstOrDefault();
                if (!string.IsNullOrEmpty(runId))
                {
                    await File.AppendAllTextAsync(debugFilePath, $"x-ms-workflow-run-id: {runId}");
                }
            }
            catch (Exception)
            {
                Console.WriteLine($"Failed to write teams message output file");
            }

            return true;
        }
        else
        {
            throw new Exception($"Teams Post Message Call Failed. Status Code : {response.StatusCode}, Error : {await response.Content.ReadAsStringAsync()}");
        }
    }
}
