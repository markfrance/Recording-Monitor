using System;
using System.ComponentModel;

namespace MSharp.Framework
{

    [Serializable]
    public class ValidationException : WarningException
    {
        public ValidationException() { }
        public ValidationException(string messageFormat, params object[] arguments) : base(string.Format(messageFormat, arguments)) { }
        public ValidationException(string message) : base(message) { }
        public ValidationException(string message, Exception inner) : base(message, inner) { }
        protected ValidationException(System.Runtime.Serialization.SerializationInfo info,
          System.Runtime.Serialization.StreamingContext context)
            : base(info, context)
        { }
        public string InvalidPropertyName { get; set; }
    }
}