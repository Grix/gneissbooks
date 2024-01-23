using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using Google.Apis.Util.Store;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BilagParser
{
    internal class GoogleSheetsApi
    {
        const string spreadsheetId = "1cigGJkBYZMlsU3HNK_rVgEhyfeiTghdyHlUyLjbiq1w";
        SheetsService? sheetsService;

        public void Initialize()
        {
            if (sheetsService != null)
                return;

            // Authenticate with google sheets
            UserCredential credential;

            using (var stream = new FileStream("credentials.json", FileMode.Open, FileAccess.Read))
            {
                // The file token.json stores the user's access and refresh tokens, and is created
                // automatically when the authorization flow completes for the first time.
                string credPath = "token.json";
                credential = GoogleWebAuthorizationBroker.AuthorizeAsync(
                    GoogleClientSecrets.FromStream(stream).Secrets,
                    new string[] { SheetsService.Scope.Spreadsheets },
                    "user",
                    CancellationToken.None,
                    new FileDataStore(credPath, true)).Result;
                Console.WriteLine("Credential file saved to: " + credPath);
            }

            sheetsService = new SheetsService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = "BilagParser",
            });
        }

        public ValueRange? ReadValuesFromSheet(string range)
        {
            if (sheetsService == null)
                throw new Exception("Has not initialized sheets API");

            SpreadsheetsResource.ValuesResource.GetRequest request = sheetsService.Spreadsheets.Values.Get(spreadsheetId, range);

            return request.Execute();
        }

        public UpdateValuesResponse? WriteValueToSheetCell(string cell, object? value)
        {
            if (sheetsService == null)
                throw new Exception("Has not initialized the sheets API service");

            var valueRange = new ValueRange();
            var objectList = new List<object>() { value ?? string.Empty };
            valueRange.Values = new List<IList<object>> { objectList };
            var update = sheetsService.Spreadsheets.Values.Update(valueRange, spreadsheetId, cell);
            update.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.USERENTERED;
            return update.Execute();
        }
    }
}
