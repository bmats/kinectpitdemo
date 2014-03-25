using System;
using System.Runtime.Serialization;

namespace Rotate3D {
    /// <summary>
    /// Thrown on errors communicating with SolidWorks.
    /// </summary>
    [Serializable]
    public class SolidWorksException : Exception {
        public SolidWorksException() { }
        public SolidWorksException(string message) : base(message) { }
        public SolidWorksException(string message, Exception inner) : base(message, inner) { }
        protected SolidWorksException(SerializationInfo info, StreamingContext context) : base(info, context) { }
    }
}
