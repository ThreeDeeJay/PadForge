using System;

namespace PadForge.Common.Input
{
    /// <summary>
    /// Event arguments for input pipeline errors.
    /// </summary>
    public class InputExceptionEventArgs : EventArgs
    {
        public string Message { get; }
        public Exception Exception { get; }

        public InputExceptionEventArgs(string message, Exception exception)
        {
            Message = message ?? string.Empty;
            Exception = exception;
        }

        public override string ToString()
        {
            if (Exception != null)
                return $"{Message}: {Exception.Message}";
            return Message;
        }
    }
}
