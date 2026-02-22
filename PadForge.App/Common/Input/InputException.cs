using System;

namespace PadForge.Common.Input
{
    /// <summary>
    /// Exception type for errors originating in the PadForge input pipeline.
    /// Provides additional context about the device and pipeline step where
    /// the error occurred.
    /// </summary>
    public class InputException : Exception
    {
        /// <summary>
        /// The instance GUID of the device that caused the error, if applicable.
        /// </summary>
        public Guid? DeviceInstanceGuid { get; }

        /// <summary>
        /// The pipeline step where the error occurred (1–6), or 0 if not step-specific.
        /// </summary>
        public int PipelineStep { get; }

        /// <summary>
        /// Creates a new InputException with a message.
        /// </summary>
        public InputException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Creates a new InputException with a message and inner exception.
        /// </summary>
        public InputException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        /// <summary>
        /// Creates a new InputException with full context.
        /// </summary>
        /// <param name="message">Error description.</param>
        /// <param name="innerException">The underlying exception.</param>
        /// <param name="deviceGuid">The device's instance GUID (if applicable).</param>
        /// <param name="pipelineStep">The pipeline step (1–6) where the error occurred.</param>
        public InputException(string message, Exception innerException,
            Guid? deviceGuid, int pipelineStep)
            : base(message, innerException)
        {
            DeviceInstanceGuid = deviceGuid;
            PipelineStep = pipelineStep;
        }

        public override string ToString()
        {
            string stepInfo = PipelineStep > 0 ? $" [Step {PipelineStep}]" : "";
            string deviceInfo = DeviceInstanceGuid.HasValue
                ? $" [Device {DeviceInstanceGuid.Value:N}]"
                : "";

            return $"InputException{stepInfo}{deviceInfo}: {Message}" +
                   (InnerException != null ? $"\n  → {InnerException.Message}" : "");
        }
    }
}
