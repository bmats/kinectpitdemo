using System;
using Microsoft.Kinect.Toolkit.Interaction;

namespace Warlords.Kinect {
    public class UserInfoReadyEventArgs : EventArgs {
        internal UserInfoReadyEventArgs(UserInfo[] userInfo) {
            this.UserInfo = userInfo;
        }

        public UserInfo[] UserInfo { get; private set; }
    }
}
