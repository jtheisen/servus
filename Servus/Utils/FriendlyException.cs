using Servus.Utils;

class FriendlyException(String? message, Exception? innerException = null)
  : Exception(message, innerException), IExceptionWithFactory<FriendlyException>
{
  static FriendlyException IExceptionWithFactory<FriendlyException>.Create(String? message, Exception? innerException)
    => new(message, innerException);
}
