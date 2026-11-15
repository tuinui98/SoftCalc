// CryptoHttp.cs - The one HTTP client the Balance tab and the watcher ask services with (design/crypto_plan.md C1-C2)
//
// A 429/503 answer, or no answer in time (a throttling service often just
// goes quiet), is a ServiceBusyException, which CryptoBalance waits on.

using SoftCalc.Logic;

namespace SoftCalc.UI;

internal static class CryptoHttp
{
	private static readonly HttpClient Http = MakeHttp();

	public static async Task<string> FetchAsync(string url, string? jsonBody, CancellationToken cancel)
	{
		using var request = new HttpRequestMessage(jsonBody == null ? HttpMethod.Get : HttpMethod.Post, url);
		if (jsonBody != null)
		{
			request.Content = new StringContent(jsonBody, System.Text.Encoding.UTF8, "application/json");
		}
		string host = new Uri(url).Host;
		try
		{
			using var response = await Http.SendAsync(request, cancel);
			string text = await response.Content.ReadAsStringAsync(cancel);
			if (response.StatusCode is System.Net.HttpStatusCode.TooManyRequests or System.Net.HttpStatusCode.ServiceUnavailable)
			{
				throw new ServiceBusyException(host, $"{(int)response.StatusCode} {response.ReasonPhrase}", response.Headers.RetryAfter?.Delta);
			}
			if (!response.IsSuccessStatusCode)
			{
				throw new HttpRequestException($"{(int)response.StatusCode} {response.ReasonPhrase} from {host}");
			}
			return text;
		}
		catch (TaskCanceledException) when (!cancel.IsCancellationRequested)
		{
			// HttpClient's own timeout, not Stop
			throw new ServiceBusyException(host, $"no answer within {Http.Timeout.TotalSeconds:0} s", null);
		}
	}

	private static HttpClient MakeHttp()
	{
		var http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
		http.DefaultRequestHeaders.UserAgent.ParseAdd("SoftCalc/1.0");
		return http;
	}
}
