using System;
using System.Runtime.Serialization;

namespace Warlords.Kinect {
    [Serializable]
    public class KinectException : Exception {
        public KinectException() { }
        public KinectException(string message) : base(message) { }
        public KinectException(string message, Exception inner) : base(message, inner) { }
        protected KinectException(SerializationInfo info, StreamingContext context) : base(info, context) { }
    }
}
