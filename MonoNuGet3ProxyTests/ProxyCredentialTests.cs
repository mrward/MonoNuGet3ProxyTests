using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using NUnit.Framework;

namespace MonoNuGet3ProxyTests
{
	public class ProxyCredentialTests
	{
		// Assume Fiddler is being used as the proxy.
		const string ProxyUserName = "1";
		const string ProxyPassword = "1";

		bool HandleRemoteCertificateValidationCallback (object sender, System.Security.Cryptography.X509Certificates.X509Certificate certificate, System.Security.Cryptography.X509Certificates.X509Chain chain, System.Net.Security.SslPolicyErrors sslPolicyErrors)
		{
			return true;
		}

		[SetUp]
		public void SetUp ()
		{
			// Workaround for Mono 4.8.0 bug: https://bugzilla.xamarin.com/show_bug.cgi?id=44972
			ServicePointManager.ServerCertificateValidationCallback = HandleRemoteCertificateValidationCallback;
		}

		[Test]
		public async Task HttpAsyncThroughProxy ()
		{
			string url = "http://www.nuget.org/api/v2";

			var request = new HttpRequestMessage (HttpMethod.Get, url);
			var handler = new HttpClientHandler {
				UseProxy = true,
				UseDefaultCredentials = true,
				Proxy = WebRequest.GetSystemWebProxy ()
			};
			var client = new HttpClient (handler);
			HttpResponseMessage response = null;
			Exception exception = null;

			try {
				response = await client.SendAsync (request);
			} catch (Exception ex) {
				exception = ex;
			}

			Assert.IsNull (exception);
			Assert.AreEqual (HttpStatusCode.ProxyAuthenticationRequired, response.StatusCode);

			// Try again using credentials.
			request = new HttpRequestMessage (HttpMethod.Get, url);
			handler.Proxy.Credentials = new NetworkCredential (ProxyUserName, ProxyPassword);
			var responseMessage = await client.SendAsync (request);

			Assert.AreEqual (HttpStatusCode.OK, responseMessage.StatusCode);
		}

		[Test]
		public async Task HttpsAsyncRequestThroughProxy ()
		{
			string url = "https://www.nuget.org/api/v2";

			var request = new HttpRequestMessage (HttpMethod.Get, url);
			var handler = new HttpClientHandler {
				UseProxy = true,
				UseDefaultCredentials = true,
				Proxy = WebRequest.GetSystemWebProxy ()
			};
			var client = new HttpClient (handler);

			Exception exceptionThrown = null;

			try {
				await client.SendAsync(request);
			} catch (Exception ex) {
				exceptionThrown = ex;
			}

			WebException webEx = exceptionThrown as WebException;
			if (webEx == null) {
				webEx = exceptionThrown.InnerException as WebException;
			}
			//var response = webEx?.Response as HttpWebResponse;
			//Assert.IsNotNull (response);
			//Assert.AreEqual (HttpStatusCode.ProxyAuthenticationRequired, response.StatusCode);
			Assert.IsNotNull (webEx);

			// Try again using credentials.
			request = new HttpRequestMessage (HttpMethod.Get, url);
			handler.Proxy.Credentials = new NetworkCredential (ProxyUserName, ProxyPassword);
			var responseMessage = await client.SendAsync (request);

			Assert.AreEqual (HttpStatusCode.OK, responseMessage.StatusCode);
		}
	}
}
