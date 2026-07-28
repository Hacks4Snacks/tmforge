namespace ThreatModelForge.Api.Tests
{
    using System;
    using System.IO;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Http;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Tests where <see cref="InputErrorHandler"/> draws the line between the caller's fault and the
    /// server's. The endpoint tests cover the first half over HTTP; this covers the second, which
    /// cannot be reached that way without an endpoint that fails on purpose. Widening the
    /// classification until every failure looks like a bad request would hide real breakage behind a
    /// 400, and nothing else in the suite would notice.
    /// </summary>
    [TestClass]
    public class InputErrorHandlerTest
    {
        /// <summary>Verifies the failures caused by a request are answered rather than left to become 500s.</summary>
        /// <returns>A task.</returns>
        [TestMethod]
        public async Task CallerInputIsHandled()
        {
            foreach (Exception error in new Exception[]
            {
                new NotSupportedException("No threat model format with id 'nonsense'."),
                new ArgumentException("Value cannot be null or empty.", "formatId"),
                new FormatException("The input is not a valid Base-64 string."),
                new JsonException("'0x01' is an invalid start of a value."),
                new BadHttpRequestException("Request body was not valid JSON.", StatusCodes.Status400BadRequest),
            })
            {
                DefaultHttpContext context = NewContext();

                bool handled = await new InputErrorHandler().TryHandleAsync(context, error, CancellationToken.None);

                Assert.IsTrue(handled, error.GetType().Name + " should be reported as a bad request.");
                Assert.AreEqual(StatusCodes.Status400BadRequest, context.Response.StatusCode);
            }
        }

        /// <summary>
        /// Verifies an unexpected failure is declined, so it still surfaces as a server error. This is
        /// the guard on the classification staying narrow.
        /// </summary>
        /// <returns>A task.</returns>
        [TestMethod]
        public async Task AnUnexpectedFailureIsLeftAsAServerError()
        {
            foreach (Exception error in new Exception[]
            {
                new InvalidOperationException("The engine reached an impossible state."),
                new NullReferenceException(),
                new IOException("The disk went away."),
            })
            {
                DefaultHttpContext context = NewContext();

                bool handled = await new InputErrorHandler().TryHandleAsync(context, error, CancellationToken.None);

                Assert.IsFalse(handled, error.GetType().Name + " is not the caller's fault and must stay a 500.");
            }
        }

        /// <summary>
        /// Verifies the status model binding already chose is preserved. Installing the handler must not
        /// re-label a request the framework had classified correctly on its own.
        /// </summary>
        /// <returns>A task.</returns>
        [TestMethod]
        public async Task AModelBindingStatusIsPreserved()
        {
            DefaultHttpContext context = NewContext();
            BadHttpRequestException error = new BadHttpRequestException(
                "Request body too large.",
                StatusCodes.Status413PayloadTooLarge);

            bool handled = await new InputErrorHandler().TryHandleAsync(context, error, CancellationToken.None);

            Assert.IsTrue(handled);
            Assert.AreEqual(StatusCodes.Status413PayloadTooLarge, context.Response.StatusCode);
        }

        /// <summary>Builds a context whose response body can be written to.</summary>
        /// <returns>The context.</returns>
        private static DefaultHttpContext NewContext()
        {
            DefaultHttpContext context = new DefaultHttpContext();
            context.Response.Body = new MemoryStream();
            context.Request.Path = "/v1/model/convert";
            return context;
        }
    }
}
