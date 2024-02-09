using Avalonia;
using GneissBooks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Transactions;

namespace GneissBooks;

internal class ExchangeRateApi
{
    static Dictionary<string, Dictionary<DateOnly, decimal>> cachedRates = new() { ["usd"] = new(), ["eur"] = new()};

    public static async Task<decimal> GetExchangeRateInNok(string currencyCode, DateTimeOffset date)
    {
        return await GetExchangeRateInNok(currencyCode, DateOnly.FromDateTime(date.Date));
    }

    public static async Task<decimal> GetExchangeRateInNok(string currencyCode, DateOnly date)
    {
        currencyCode = currencyCode.ToLower();

        if (cachedRates[currencyCode].ContainsKey(date))
            return cachedRates[currencyCode][date];

        string url = $"http://api.exchangeratesapi.io/{date.ToString("yyyy-MM-dd")}?access_key={App.UserData["ExchangeRateApiKey"]}";

        using (HttpClient client = new HttpClient())
        {
            HttpResponseMessage response = await client.GetAsync(url);
            if (response.IsSuccessStatusCode)
            {
                string result = await response.Content.ReadAsStringAsync();
                var jsonResult = JsonNode.Parse(result);
                var rate = jsonResult!["rates"]!["NOK"]!.GetValue<decimal>() / jsonResult["rates"]![currencyCode.ToUpper()]!.GetValue<decimal>();
                if (rate <= 0)
                {
                    throw new Exception("Unexpected " + currencyCode + " exchange rate: " + rate);
                }
                cachedRates[currencyCode][date] = rate;
                return rate;
            }
            else
            {
                throw new Exception($"Error fetching exchange rate: {response.StatusCode}");
            }
        }
    }
}
