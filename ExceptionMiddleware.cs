using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace ExceptionMiddleware
{
	/// <summary>
	/// The ExceptionMiddleware class is used to catch exceptions that originate from controller entry points.
	/// This eliminates the need to have exception handlers at every entry point and instead just rely on a single
	/// point in the pipeline to catch, report, and respond to exceptions.
	/// </summary>
	public class ExceptionMiddleware
	{
		public ExceptionMiddleware(RequestDelegate nextDelegate, ILogger<ExceptionMiddleware> logger)
		{
			this.nextDelegate = nextDelegate;
			this.logger = logger;
		}

		public async Task InvokeAsync(HttpContext httpContext)
		{
			try
			{
				await nextDelegate(httpContext);
			}
			catch (Exception ex)
			{
				await handleException(httpContext, ex, logger);
			}
		}

		/// <summary>
		/// HandleException method is expected to throw exceptions as-necessary, and the preferred type of exception to throw is
		/// the "StatusCodeException" which is an extension on the base Exception type with the addition of an action
		/// specific code to propagate.
		/// </summary>
		/// <param name="context">Current HTTP context</param>
		/// <param name="exception">Exception to handle</param>
		/// <returns>Task</returns>
		private Task handleException(HttpContext context, Exception exception, ILogger<ExceptionMiddleware> logger)
		{
			if ((context.Response.StatusCode == 0) || (context.Response.StatusCode == StatusCodes.Status200OK))
				context.Response.StatusCode = StatusCodes.Status500InternalServerError;

			XElement error = new XElement("Error",
			   new XAttribute("statusCode", context.Response.StatusCode),
			   new XElement("Request", context.GetRequestURL()),
			   new XElement("Message", string.Join(Environment.NewLine, exception.Flatten().Select(x => x.Message))),
			   new XElement("StackTrace", exception.StackTrace));

			string errorMessage = error.ToString();
			logger.LogError(errorMessage);

			return context.Response.WriteAsync(errorMessage);
		}

		private readonly RequestDelegate nextDelegate;
		private readonly ILogger<ExceptionMiddleware> logger;
	}

	public static class ConfigurationExtention
	{
		public static string GetRequestURL(this HttpContext context) =>
		(new UriBuilder
		{
			Scheme = context.Request.Scheme,
			Host = context.Request.Host.Value,
			Path = $"{context.Request.PathBase}{context.Request.Path}",
			Query = context.Request.QueryString.Value
		}).ToString();
	}

	internal static class ExceptionExtensions
	{
		internal static IEnumerable<Exception> Flatten(this Exception ex)
		{
			do { yield return ex; } while ((ex = ex.InnerException) is not null);
		}
	}
}
