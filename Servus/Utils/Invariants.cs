using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Servus.Utils;

interface IExceptionWithFactory<TException>
  where TException : Exception
{
	abstract static TException Create(String? message, Exception? nestedException = null);
}

class ApplicationAssertionFailedException(
  String? message = "Assertion failed", Exception? innerException = null, String? detailMessage = null
)
  : Exception(message, innerException), IExceptionWithFactory<ApplicationAssertionFailedException>
{
	public String? DetailMessage => detailMessage;

	static ApplicationAssertionFailedException IExceptionWithFactory<ApplicationAssertionFailedException>.Create(String? message, Exception? nestedException)
	  => new(message ?? "Assertion failed", nestedException);
}

static class Invariants
{
	[OverloadResolutionPriority(-1)]
	public static void Assert([DoesNotReturnIf(false)] bool condition)
	{
		if (!condition)
		{
			throw new ApplicationAssertionFailedException();
		}
	}

	public static void Assert([DoesNotReturnIf(false)] bool condition, [CallerArgumentExpression("condition")] string? message = null)
	{
		if (!condition)
		{
			throw new ApplicationAssertionFailedException(message);
		}
	}

	public static void Assert([DoesNotReturnIf(false)] bool condition, string? message, string? detailMessage)
	{
		if (!condition)
		{
			throw new ApplicationAssertionFailedException(message, null, detailMessage);
		}
	}

	public static void Assert<TException>([DoesNotReturnIf(false)] bool condition, [CallerArgumentExpression("condition")] string? message = null)
	  where TException : Exception, IExceptionWithFactory<TException>
	{
		if (!condition)
		{
			TException.Create(message);
		}
	}
}
