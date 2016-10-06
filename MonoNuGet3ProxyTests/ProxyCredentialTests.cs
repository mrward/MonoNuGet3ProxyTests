using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
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

		class TestCredentialService : ICredentialService
		{
			public Task<ICredentials> GetCredentialsAsync (Uri uri, IWebProxy proxy, CredentialRequestType type, string message, CancellationToken cancellationToken)
			{
				if (type == CredentialRequestType.Proxy)
					return Task.FromResult (new NetworkCredential (ProxyUserName, ProxyPassword) as ICredentials);

				return Task.FromResult ((ICredentials)null);
			}
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

			Assert.IsNotNull (exceptionThrown);

			providers = Repository.Provider.GetCoreV3 ().ToList ();
			repository = new SourceRepository (source, providers);

			HttpHandlerResourceV3.CredentialService = new TestCredentialService ();

			var resource2 = await repository.GetResourceAsync<PackageMetadataResource> ();
			var metadata2 = await resource2.GetMetadataAsync ("Xamarin.Forms", false, false, NullLogger.Instance, CancellationToken.None);
			Assert.IsNotNull (metadata2);
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
