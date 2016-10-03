using NUnit.Framework;
using NuGet.Protocol.Core.Types;
using NuGet.Configuration;
using NuGet.Protocol;
using System;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Common;
using System.Threading;
using System.Net;
using System.Net.Http;

namespace MonoNuGet3ProxyTests
{
	[TestFixture]
	public class ProxyTests
	{
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
		public async Task NuGet3Api ()
		{
			var source = new PackageSource ("https://api.nuget.org/v3/index.json") {
				ProtocolVersion = 3
			};
			var providers = Repository.Provider.GetCoreV3 ().ToList ();
			var repository = new SourceRepository (source, providers);


			Exception exceptionThrown = null;
			try {
				var resource = await repository.GetResourceAsync<PackageMetadataResource> ();
				var metadata = await resource.GetMetadataAsync ("Xamarin.Forms", false, false, NullLogger.Instance, CancellationToken.None);
				metadata.ToList ();
			} catch (Exception ex) {
				exceptionThrown = ex;
			}

			var httpEx = exceptionThrown as HttpRequestException;
			var webException = exceptionThrown?.InnerException as WebException;
			var response = webException?.Response as HttpWebResponse;

			Assert.IsNotNull (exceptionThrown);
			Assert.AreEqual (HttpStatusCode.ProxyAuthenticationRequired, response.StatusCode);
			Assert.IsNotNull (httpEx);
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

			var httpEx = exceptionThrown as HttpRequestException;
			var webEx = exceptionThrown.InnerException as WebException;
			var response = webEx?.Response as HttpWebResponse;
			Assert.IsNotNull (response);
			Assert.AreEqual (HttpStatusCode.ProxyAuthenticationRequired, response.StatusCode);
			Assert.IsNotNull (httpEx);
		}
	}
}
