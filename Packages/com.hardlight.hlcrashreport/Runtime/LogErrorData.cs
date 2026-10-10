namespace Hardlight
{
    // HLCrashReport.Runtime 02000008/0600000f. The readonly struct naturally
    // emits the original IsReadOnlyAttribute and preserves every supplied value.
    public readonly struct LogErrorData
    {
        public readonly string ErrorType;
        public readonly string ErrorMessage;
        public readonly int ErrorCode;
        public readonly string StackTrace;

        public LogErrorData(string errorType, string errorMessage, int errorCode, string stackTrace)
        {
            ErrorType = errorType;
            ErrorMessage = errorMessage;
            ErrorCode = errorCode;
            StackTrace = stackTrace;
        }
    }
}
