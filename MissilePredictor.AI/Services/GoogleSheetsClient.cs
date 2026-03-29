using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using Microsoft.Extensions.Options;
using MissilePredictor.AI.Config;

namespace MissilePredictor.AI.Services;

public class GoogleSheetsClient
{
    private readonly SheetsService _service;

    public GoogleSheetsClient(IOptions<GoogleConfig> config)
    {
        var credential = GoogleCredential.FromFile(config.Value.CredentialsFilePath)
            .CreateScoped(SheetsService.Scope.Spreadsheets);

        _service = new SheetsService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "MissilePredictor Sheets Client",
        });
    }

    public async Task<IList<IList<object>>> ReadDataAsync(string spreadsheetId, string range)
    {
        var request = _service.Spreadsheets.Values.Get(spreadsheetId, range);
        var response = await request.ExecuteAsync();
        return response.Values;
    }

    public async Task WriteDataAsync(string spreadsheetId, string range, IList<IList<object>> values)
    {
        var valueRange = new ValueRange { Values = values };
        var updateRequest = _service.Spreadsheets.Values.Update(valueRange, spreadsheetId, range);
        updateRequest.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.USERENTERED;
        await updateRequest.ExecuteAsync();
    }

    public async Task AppendDataAsync(string spreadsheetId, string range, IList<IList<object>> values)
    {
        var valueRange = new ValueRange { Values = values };
        var appendRequest = _service.Spreadsheets.Values.Append(valueRange, spreadsheetId, range);
        appendRequest.ValueInputOption = SpreadsheetsResource.ValuesResource.AppendRequest.ValueInputOptionEnum.USERENTERED;
        await appendRequest.ExecuteAsync();
    }
}