using System;

namespace Brisk.Actions
{
    [AttributeUsage(AttributeTargets.Method)]
    public class GlobalAction : Action
    {
        public GlobalAction(bool self = true)
        {
            Self = self;
        }
    }
}